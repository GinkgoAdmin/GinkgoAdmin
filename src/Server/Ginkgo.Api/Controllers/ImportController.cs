using System.Text.Json;
using System.Data;
using System.Linq;
using Ginkgo.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/import")]
[ApiVersion("1.0")]
[Authorize]
public sealed class ImportController : ControllerBase
{
    private readonly ISqlSugarClient _db;
    private readonly IBulkInsertService _bulk;
    private static readonly HashSet<string> _allowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        // 安全白名单：按需添加允许导入的表名
        "ginkgo_demo_datatable"
    };

    public ImportController(ISqlSugarClient db, IBulkInsertService bulk)
    {
        _db = db;
        _bulk = bulk;
    }

    public sealed class ImportResultDto
    {
        public int Success { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 通用导入：Body 支持数组([{...},{...}])，或 { rows: [...]} / { data: [...] }
    /// 字段自动过滤为目标表存在的列，并尝试进行基本类型转换。
    /// </summary>
    [HttpPost("{table}")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<ActionResult<ImportResultDto>> ImportAsync(string table, [FromBody] JsonElement payload)
    {
        if (string.IsNullOrWhiteSpace(table)) return BadRequest("缺少表名");

        // 严格白名单校验：仅允许导入到明确列出的业务表，禁止写入任意存在表
        if (!_allowedTables.Contains(table))
            return BadRequest($"不允许导入到表: {table}");

        var rows = ParseRows(payload);
        if (rows.Count == 0) return BadRequest("未解析到任何数据行");

        var cols = _db.DbMaintenance.GetColumnInfosByTableName(table, true)
            .ToDictionary(c => c.DbColumnName, StringComparer.OrdinalIgnoreCase);

        var prepared = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in row)
            {
                if (!cols.TryGetValue(kv.Key, out var col)) continue; // 过滤不存在列
                dict[col.DbColumnName] = ConvertToColumnType(kv.Value, col);
            }
            if (dict.Count > 0) prepared.Add(dict);
        }

        var result = new ImportResultDto();
        if (prepared.Count == 0) return Ok(result);

        // 使用 Fastest BulkCopy（无行数限制），并包裹事务，失败则整体回滚
        try
        {
            // 拆分两批：有 id 的行（显式插入 id），无 id 的行（让 DEFAULT 生成）
            var withId = new List<Dictionary<string, object?>>(prepared.Count);
            var withoutId = new List<Dictionary<string, object?>>(prepared.Count);
            foreach (var r in prepared)
            {
                if (r.TryGetValue("id", out var idVal) && idVal != null && !string.IsNullOrWhiteSpace(Convert.ToString(idVal)))
                    withId.Add(r);
                else
                    withoutId.Add(r);
            }

            DataTable BuildDataTable(List<Dictionary<string, object?>> rows)
            {
                var dtLocal = new DataTable(table);
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rr in rows) foreach (var k in rr.Keys) used.Add(k);
                var sel = cols.Values.Where(c => used.Contains(c.DbColumnName)).ToList();
                foreach (var col in sel)
                {
                    var type = MapClrType(col);
                    dtLocal.Columns.Add(col.DbColumnName, type);
                }
                foreach (var rr in rows)
                {
                    var dr = dtLocal.NewRow();
                    foreach (DataColumn c in dtLocal.Columns)
                    {
                        dr[c.ColumnName] = rr.TryGetValue(c.ColumnName, out var v) && v != null ? v : DBNull.Value;
                    }
                    dtLocal.Rows.Add(dr);
                }
                return dtLocal;
            }

            var tran = await _db.Ado.UseTranAsync(async () =>
            {
                if (withId.Count > 0)
                {
                    var dtWith = BuildDataTable(withId); // 包含 id 列
                    // 统一走 IBulkInsertService：运行时可被 Database.Features.BulkOps 开关控制（Enabled=false 时降级为逐行 Insertable）。
                    var aff = await _bulk.BulkInsertDataTableAsync(table, dtWith).ConfigureAwait(false);
                    result.Success += aff;
                }
                if (withoutId.Count > 0)
                {
                    // 确保不包含 id 列，让 DEFAULT 生成
                    var pruned = withoutId.Select(r => r.Where(kv => !kv.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
                                                       .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)).ToList();
                    // 使用 Insertable 指定列清单，确保 DEFAULT(NEWSEQUENTIALID()) 生效；该路径与 BulkOps 开关无关（必须往表设计默认值）。
                    var aff = await _db.Insertable(pruned).AS(table).ExecuteCommandAsync().ConfigureAwait(false);
                    result.Success += aff;
                }
            });
            if (tran.IsSuccess)
            {
                result.Failed = 0;
                return Ok(result);
            }
            else
            {
                return BadRequest(new ImportResultDto { Success = 0, Failed = prepared.Count, Errors = new List<string> { tran.ErrorMessage ?? "导入失败" } });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new ImportResultDto { Success = 0, Failed = prepared.Count, Errors = new List<string> { ex.Message } });
        }
    }

    private static List<Dictionary<string, object?>> ParseRows(JsonElement payload)
    {
        var list = new List<Dictionary<string, object?>>(256);
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
            {
                var d = ToDictionary(item);
                if (d != null) list.Add(d);
            }
            return list;
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in rows.EnumerateArray())
                {
                    var d = ToDictionary(item);
                    if (d != null) list.Add(d);
                }
                return list;
            }
            if (payload.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var d = ToDictionary(item);
                    if (d != null) list.Add(d);
                }
                return list;
            }
            // 单对象也允许
            var single = ToDictionary(payload);
            if (single != null) list.Add(single);
        }
        return list;
    }

    private static Dictionary<string, object?>? ToDictionary(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in obj.EnumerateObject())
        {
            d[p.Name] = FromJson(p.Value);
        }
        return d;
    }

    private static object? FromJson(JsonElement v)
    {
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.TryGetInt64(out var li) ? li : (v.TryGetDouble(out var d) ? d : (object?)null),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => v.EnumerateArray().Select(FromJson).ToArray(),
            JsonValueKind.Object => v.ToString(), // 内嵌对象以 JSON 字符串落库
            _ => null
        };
    }

    private static object? ConvertToColumnType(object? value, DbColumnInfo col)
    {
        if (value == null) return null;
        try
        {
            var t = MapClrType(col);
            if (t == typeof(string)) return Convert.ToString(value);
            if (t == typeof(int)) return Convert.ToInt32(value);
            if (t == typeof(long)) return Convert.ToInt64(value);
            if (t == typeof(bool))
            {
                if (value is string sb)
                {
                    if (string.Equals(sb, "true", StringComparison.OrdinalIgnoreCase) || sb == "1" || sb == "是") return true;
                    if (string.Equals(sb, "false", StringComparison.OrdinalIgnoreCase) || sb == "0" || sb == "否") return false;
                }
                return Convert.ToBoolean(value);
            }
            if (t == typeof(decimal)) return Convert.ToDecimal(value);
            if (t == typeof(double)) return Convert.ToDouble(value);
            if (t == typeof(float)) return Convert.ToSingle(value);
            if (t == typeof(DateTime))
            {
                if (value is string s && DateTime.TryParse(s, out var dt)) return dt;
                if (value is long ticks) return DateTimeOffset.FromUnixTimeMilliseconds(ticks).DateTime;
            }
            if (t == typeof(TimeSpan))
            {
                if (value is string s && TimeSpan.TryParse(s, out var ts)) return ts;
            }
            return value;
        }
        catch
        {
            return value;
        }
    }

    private static Type MapClrType(DbColumnInfo col)
    {
        var t = col.PropertyType;
        if (t == null || t == typeof(object))
        {
            t = MapTypeFromDbType(col.DataType);
        }
        return t ?? typeof(string);
    }

    private static Type MapTypeFromDbType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType)) return typeof(string);
        switch (dbType.Trim().ToLowerInvariant())
        {
            case "int":
            case "int32":
            case "integer":
                return typeof(int);
            case "bigint":
            case "int64":
                return typeof(long);
            case "bit":
            case "bool":
            case "boolean":
                return typeof(bool);
            case "decimal":
            case "numeric":
            case "money":
                return typeof(decimal);
            case "float":
            case "real":
                return typeof(double);
            case "double":
                return typeof(double);
            case "date":
            case "datetime":
            case "datetime2":
            case "smalldatetime":
            case "timestamp":
                return typeof(DateTime);
            case "time":
                return typeof(TimeSpan);
            case "varchar":
            case "nvarchar":
            case "char":
            case "nchar":
            case "text":
            case "ntext":
            default:
                return typeof(string);
        }
    }
}


