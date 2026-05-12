using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 通用实体查询接口（供 EntityPicker 组件使用）。
/// 仅允许查询 ginkgo_ 前缀的表，字段名严格校验防止注入。
/// </summary>
[ApiController]
[Route("api/system")]
[Authorize]
public sealed class EntityLookupController : ControllerBase
{
    private static readonly Regex SafeFieldRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]{0,63}$", RegexOptions.Compiled);
    private static readonly Regex SafeTableRegex = new(@"^ginkgo_[a-zA-Z0-9_]{1,100}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 通用实体数据查询（分页 + 关键词搜索）。
    /// </summary>
    /// <param name="db">SqlSugar 数据库实例。</param>
    /// <param name="table">表名（必须以 ginkgo_ 开头）。</param>
    /// <param name="valueField">值字段名，默认 Id。</param>
    /// <param name="labelField">显示字段名，默认 Name。</param>
    /// <param name="keyword">搜索关键词（模糊匹配 labelField）。</param>
    /// <param name="page">页码，默认 1。</param>
    /// <param name="pageSize">每页条数，默认 10，最大 100。</param>
    [HttpGet("entity-lookup")]
    public async Task<IActionResult> EntityLookup(
        [FromServices] ISqlSugarClient db,
        [FromQuery] string table,
        [FromQuery] string? valueField = "Id",
        [FromQuery] string? labelField = "Name",
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(table))
            return BadRequest(new { code = 400, message = "table 参数不能为空" });

        if (!SafeTableRegex.IsMatch(table))
            return BadRequest(new { code = 400, message = "table 参数不合法，必须以 ginkgo_ 开头且只能包含字母、数字和下划线" });

        var vField = string.IsNullOrWhiteSpace(valueField) ? "Id" : valueField;
        var lField = string.IsNullOrWhiteSpace(labelField) ? "Name" : labelField;

        if (!SafeFieldRegex.IsMatch(vField))
            return BadRequest(new { code = 400, message = "valueField 参数不合法" });

        if (!SafeFieldRegex.IsMatch(lField))
            return BadRequest(new { code = 400, message = "labelField 参数不合法" });

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        try
        {
            // 检测表是否存在
            var tableExists = db.DbMaintenance.GetTableInfoList(false)
                .Any(t => string.Equals(t.Name, table, StringComparison.OrdinalIgnoreCase));
            if (!tableExists)
                return NotFound(new { code = 404, message = $"表 {table} 不存在" });

            // 检测字段是否存在
            var columns = db.DbMaintenance.GetColumnInfosByTableName(table, false);
            var colNames = columns.Select(c => c.DbColumnName).ToList();

            var vFieldActual = colNames.FirstOrDefault(c => string.Equals(c, vField, StringComparison.OrdinalIgnoreCase));
            var lFieldActual = colNames.FirstOrDefault(c => string.Equals(c, lField, StringComparison.OrdinalIgnoreCase));

            if (vFieldActual == null)
                return BadRequest(new { code = 400, message = $"字段 {vField} 在表 {table} 中不存在" });

            // labelField 可能不存在，退化为 valueField
            if (lFieldActual == null) lFieldActual = vFieldActual;

            // 检测是否有软删除字段
            var hasIsDeleted = colNames.Any(c => string.Equals(c, "IsDeleted", StringComparison.OrdinalIgnoreCase));

            // 使用参数化查询
            var selectSql = $"SELECT `{vFieldActual}`, `{lFieldActual}` FROM `{table}`";
            var conditions = new List<string>();
            var parameters = new List<SugarParameter>();

            if (hasIsDeleted)
            {
                conditions.Add("`IsDeleted` = @isDeleted");
                parameters.Add(new SugarParameter("@isDeleted", false));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                conditions.Add($"`{lFieldActual}` LIKE @keyword");
                parameters.Add(new SugarParameter("@keyword", $"%{keyword.Trim()}%"));
            }

            var whereSql = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";

            // 总数
            var countSql = $"SELECT COUNT(*) FROM `{table}`{whereSql}";
            var totalCount = await db.Ado.GetIntAsync(countSql, parameters.ToArray());

            // 分页数据
            var offset = (page - 1) * pageSize;
            var dataSql = $"{selectSql}{whereSql} ORDER BY `{vFieldActual}` LIMIT @offset, @pageSize";
            parameters.Add(new SugarParameter("@offset", offset));
            parameters.Add(new SugarParameter("@pageSize", pageSize));

            var rows = await db.Ado.SqlQueryAsync<dynamic>(dataSql, parameters.ToArray());

            // 统一输出格式
            var items = rows.Select(r =>
            {
                var dict = (IDictionary<string, object>)r;
                return new Dictionary<string, object?>
                {
                    ["id"] = dict.ContainsKey(vFieldActual) ? dict[vFieldActual]?.ToString() : null,
                    [ToCamelCase(lFieldActual)] = dict.ContainsKey(lFieldActual) ? dict[lFieldActual] : null
                };
            }).ToList();

            return Ok(new { items, total = totalCount });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = 500, message = $"查询失败：{ex.Message}" });
        }
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
