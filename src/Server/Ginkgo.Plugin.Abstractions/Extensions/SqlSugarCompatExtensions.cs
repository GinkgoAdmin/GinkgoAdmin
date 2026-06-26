// 跨数据库 SqlSugar 查询/SQL 片段兼容辅助（MySQL ↔ PostgreSQL）。

using SqlSugar;

namespace Ginkgo.Plugin.Abstractions.Extensions;

/// <summary>
/// SqlSugar 跨方言兼容扩展：软删除条件、布尔字面量、标识符引用等。
/// </summary>
public static class SqlSugarCompatExtensions
{
    /// <summary>当前连接是否为 PostgreSQL。</summary>
    public static bool IsPostgreSql(this ISqlSugarClient db)
        => db.CurrentConnectionConfig.DbType == DbType.PostgreSQL;

    /// <summary>当前连接是否为 SQL Server。</summary>
    public static bool IsSqlServer(this ISqlSugarClient db)
        => db.CurrentConnectionConfig.DbType == DbType.SqlServer;

    /// <summary>当前连接是否为 MySQL。</summary>
    public static bool IsMySql(this ISqlSugarClient db)
        => db.CurrentConnectionConfig.DbType == DbType.MySql;

    /// <summary>引用列名（PG 双引号 / MySQL 反引号 / SQL Server 方括号）。</summary>
    public static string QuoteCol(this ISqlSugarClient db, string columnName)
    {
        if (db.IsPostgreSql())
            return $"\"{columnName.Replace("\"", "\"\"")}\"";
        if (db.IsSqlServer())
            return $"[{columnName.Replace("]", "]]")}]";
        return $"`{columnName.Replace("`", "``")}`";
    }

    /// <summary>SELECT 结果列别名（PostgreSQL 未加引号时会被折叠为小写，导致 ORM/dynamic 映射失败）。</summary>
    public static string QuoteAlias(this ISqlSugarClient db, string aliasName)
        => db.QuoteCol(aliasName);

    /// <summary>SELECT 表达式 AS 别名。</summary>
    public static string SelectAs(this ISqlSugarClient db, string expression, string aliasName)
        => $"{expression} AS {db.QuoteAlias(aliasName)}";

    /// <summary>布尔字面量（PG TRUE/FALSE，MySQL 1/0）。</summary>
    public static string BoolSql(this ISqlSugarClient db, bool value)
        => db.IsPostgreSql() ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");

    /// <summary>未软删除条件，用于拼接原生 ADO / 字符串 Where。</summary>
    public static string NotDeletedSql(this ISqlSugarClient db, string? tableAlias = null)
    {
        if (db.IsPostgreSql())
        {
            return string.IsNullOrWhiteSpace(tableAlias)
                ? "\"IsDeleted\" = FALSE"
                : $"\"{tableAlias}\".\"IsDeleted\" = FALSE";
        }

        if (db.IsSqlServer())
        {
            return string.IsNullOrWhiteSpace(tableAlias)
                ? "[IsDeleted] = 0"
                : $"{tableAlias}.[IsDeleted] = 0";
        }

        return string.IsNullOrWhiteSpace(tableAlias)
            ? "`IsDeleted` = 0"
            : $"{tableAlias}.`IsDeleted` = 0";
    }

    /// <summary>引用表名（PG 双引号 / MySQL 反引号 / SQL Server 方括号）。</summary>
    public static string QuoteTable(this ISqlSugarClient db, string tableName)
    {
        if (db.IsPostgreSql())
            return $"\"{tableName.Replace("\"", "\"\"")}\"";
        if (db.IsSqlServer())
            return $"[{tableName.Replace("]", "]]")}]";
        return $"`{tableName.Replace("`", "``")}`";
    }

    /// <summary>按天格式化日期列（用于 GROUP BY 趋势统计）。</summary>
    public static string DateFormatDaySql(this ISqlSugarClient db, string columnExpression)
        => db.IsPostgreSql()
            ? $"TO_CHAR({columnExpression}, 'YYYY-MM-DD')"
            : $"DATE_FORMAT({columnExpression}, '%Y-%m-%d')";

    /// <summary>日期列距今天数（用于过期提醒）。</summary>
    public static string DayDiffFromNowSql(this ISqlSugarClient db, string columnExpression)
        => db.IsPostgreSql()
            ? $"({columnExpression}::date - CURRENT_DATE)"
            : $"DATEDIFF({columnExpression}, NOW())";

    /// <summary>带表别名的列引用。</summary>
    public static string QualifyCol(this ISqlSugarClient db, string tableAlias, string columnName)
    {
        if (db.IsPostgreSql())
            return $"\"{tableAlias}\".\"{columnName.Replace("\"", "\"\"")}\"";
        if (db.IsSqlServer())
            return $"{tableAlias}.[{columnName.Replace("]", "]]")}]";
        return $"{tableAlias}.`{columnName.Replace("`", "``")}`";
    }

    /// <summary>BIGINT 主键列转文本（雪花 ID 前端字符串化）。</summary>
    public static string CastBigIntAsText(this ISqlSugarClient db, string columnExpression)
    {
        if (db.IsPostgreSql()) return $"{columnExpression}::text";
        if (db.IsSqlServer()) return $"CAST({columnExpression} AS VARCHAR(32))";
        return $"CAST({columnExpression} AS CHAR)";
    }

    /// <summary>带表别名的 BIGINT 主键列转文本。</summary>
    public static string CastBigIntAsText(this ISqlSugarClient db, string? tableAlias, string columnName)
    {
        var col = string.IsNullOrWhiteSpace(tableAlias)
            ? db.QuoteCol(columnName)
            : db.QualifyCol(tableAlias, columnName);
        return db.CastBigIntAsText(col);
    }

    /// <summary>分页 LIMIT 子句（参数化：MySQL LIMIT offset,count / PG LIMIT count OFFSET offset）。</summary>
    public static string PagedLimitSql(this ISqlSugarClient db)
        => db.IsPostgreSql() ? "LIMIT @pageSize OFFSET @offset" : "LIMIT @offset, @pageSize";

    /// <summary>分页 LIMIT 子句（字面量 offset/size）。</summary>
    public static string PagedLimitLiteral(this ISqlSugarClient db, int offset, int size)
        => db.IsPostgreSql() ? $"LIMIT {size} OFFSET {offset}" : $"LIMIT {offset}, {size}";

    /// <summary>BIGINT 参数（PostgreSQL 须显式 Int64，否则易被推断为 text）。</summary>
    public static SugarParameter LongParam(this ISqlSugarClient db, string name, long? value)
    {
        var p = new SugarParameter(name, value ?? (object)DBNull.Value);
        if (db.IsPostgreSql()) p.DbType = System.Data.DbType.Int64;
        return p;
    }

    /// <summary>INT 参数（PostgreSQL 显式 Int32）。</summary>
    public static SugarParameter IntParam(this ISqlSugarClient db, string name, int value)
    {
        var p = new SugarParameter(name, value);
        if (db.IsPostgreSql()) p.DbType = System.Data.DbType.Int32;
        return p;
    }

    /// <summary>布尔列参数值（PG bool，MySQL/SQL Server 用 0/1）。</summary>
    public static object BoolParamValue(this ISqlSugarClient db, bool value)
        => db.IsPostgreSql() ? value : (value ? 1 : 0);

    /// <summary>JSON 列写入表达式（PG 用 jsonb CAST，MySQL 等保持参数占位）。</summary>
    public static string JsonParamSql(this ISqlSugarClient db, string parameterName)
        => db.IsPostgreSql() ? $"CAST({parameterName} AS jsonb)" : parameterName;

    /// <summary>JSON 列 SELECT 表达式（PG 转 text 便于字符串映射）。</summary>
    public static string JsonSelectSql(this ISqlSugarClient db, string columnExpression)
        => db.IsPostgreSql() ? $"{columnExpression}::text" : columnExpression;

    /// <summary>将字典转为 SqlSugar 参数（BIGINT/INT 在 PG 上自动显式类型）。</summary>
    public static SugarParameter[] ToSugarParameters(this ISqlSugarClient db, IDictionary<string, object> source)
    {
        var list = new List<SugarParameter>(source.Count);
        foreach (var kv in source)
        {
            var name = kv.Key.StartsWith('@') ? kv.Key : "@" + kv.Key;
            switch (kv.Value)
            {
                case long l:
                    list.Add(db.LongParam(name, l));
                    break;
                case int i:
                    list.Add(db.IntParam(name, i));
                    break;
                default:
                    list.Add(new SugarParameter(name, kv.Value));
                    break;
            }
        }
        return list.ToArray();
    }

    /// <summary>合并多个 SugarParameter 数组。</summary>
    public static SugarParameter[] MergeParams(params SugarParameter[][] groups)
        => groups.SelectMany(x => x).ToArray();
}
