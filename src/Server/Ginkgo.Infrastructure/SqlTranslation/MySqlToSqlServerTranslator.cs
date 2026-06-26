// MySQL 安装脚本 → SQL Server 的轻量 DDL/DML 转写器（覆盖插件 install.sql 常见模式）。

using System.Text;
using System.Text.RegularExpressions;

namespace Ginkgo.Infrastructure.SqlTranslation;

/// <summary>
/// MySQL 5.7 常见 DDL/DML 到 SQL Server 的机械化转写。
/// </summary>
public static class MySqlToSqlServerTranslator
{
    private static readonly Regex SessionPragmaRegex = new(
        @"^SET\s+(NAMES|FOREIGN_KEY_CHECKS|CHARACTER\s+SET|COLLATION|SQL_MODE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>尝试将单条 MySQL SQL 批次转写为 SQL Server 语法。</summary>
    public static string Translate(string mysqlSql)
    {
        var sql = (mysqlSql ?? string.Empty).Trim();
        if (sql.Length == 0) return sql;

        if (SessionPragmaRegex.IsMatch(sql))
            return "-- skipped mysql session pragma";

        if (Regex.IsMatch(sql, @"^CREATE\s+TABLE\b", RegexOptions.IgnoreCase))
            return TranslateCreateTable(sql);

        return ApplyCommonReplacements(sql);
    }

    private static string TranslateCreateTable(string sql)
    {
        sql = StripColumnComments(sql);
        sql = Regex.Replace(sql, @"UNIQUE\s+KEY\s+`[^`]+`\s*\(", "UNIQUE (", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @",\s*KEY\s+`[^`]+`\s*\([^)]+\)", string.Empty, RegexOptions.IgnoreCase);
        sql = ApplyCommonReplacements(sql);
        sql = Regex.Replace(sql, @"\)\s*ENGINE\s*=\s*InnoDB[^;]*", ")", RegexOptions.IgnoreCase);
        return sql.Trim();
    }

    private static string StripColumnComments(string sql)
        => Regex.Replace(sql, @"\s+COMMENT\s+'(?:''|[^'])*'", string.Empty, RegexOptions.IgnoreCase);

    private static string ApplyCommonReplacements(string sql)
    {
        sql = Regex.Replace(sql, "`([^`]+)`", "[$1]");
        sql = Regex.Replace(sql, @"\bTINYINT\s*\(\s*1\s*\)", "BIT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bTINYINT\b", "SMALLINT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME\s*\(\s*6\s*\)", "DATETIME2(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME\b", "DATETIME2(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bLONGTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bMEDIUMTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bJSON\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDOUBLE\b", "FLOAT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bINT\b", "INT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBIGINT\b", "BIGINT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bUNSIGNED\b", string.Empty, RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\s+ON\s+UPDATE\s+CURRENT_TIMESTAMP(?:\(\d+\))?", string.Empty, RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bCURRENT_TIMESTAMP(?:\(\d+\))?\b", "SYSDATETIME()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"INSERT\s+IGNORE\s+INTO\b", "INSERT INTO", RegexOptions.IgnoreCase);
        return sql.Trim();
    }
}
