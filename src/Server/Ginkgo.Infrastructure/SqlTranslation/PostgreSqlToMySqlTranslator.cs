// PostgreSQL 常见 DDL/DML → MySQL 5.7+ 的机械化转写（覆盖插件 install.sql / 结构导出常见模式）。

using System.Text;
using System.Text.RegularExpressions;

namespace Ginkgo.Infrastructure.SqlTranslation;

/// <summary>
/// PostgreSQL 安装脚本到 MySQL 的轻量转写器。
/// </summary>
public static class PostgreSqlToMySqlTranslator
{
    private static readonly Regex SessionPragmaRegex = new(
        @"^SET\s+(session_replication_role|search_path)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>尝试将单条 PostgreSQL SQL 批次转写为 MySQL 语法。</summary>
    public static string Translate(string pgSql)
    {
        var sql = (pgSql ?? string.Empty).Trim();
        if (sql.Length == 0) return sql;

        if (SessionPragmaRegex.IsMatch(sql))
            return "-- skipped postgresql session pragma";

        if (Regex.IsMatch(sql, @"^CREATE\s+TABLE\b", RegexOptions.IgnoreCase))
            return TranslateCreateTable(sql);

        return ApplyCommonReplacements(sql);
    }

    private static string TranslateCreateTable(string sql)
    {
        sql = Regex.Replace(sql, @"\bCREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\b", "CREATE TABLE IF NOT EXISTS", RegexOptions.IgnoreCase);
        sql = ApplyCommonReplacements(sql);
        sql = Regex.Replace(sql, @"\)\s*;", ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;", RegexOptions.IgnoreCase);
        return sql.Trim();
    }

    private static string ApplyCommonReplacements(string sql)
    {
        sql = Regex.Replace(sql, @"""([^""]+)""", "`$1`");
        sql = Regex.Replace(sql, @"\bBOOLEAN\b", "TINYINT(1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bJSONB\b", "JSON", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bTIMESTAMP\s*\(\s*6\s*\)", "DATETIME(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bTIMESTAMP\b", "DATETIME(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bINTEGER\b", "INT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDOUBLE\s+PRECISION\b", "DOUBLE", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBYTEA\b", "LONGBLOB", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSERIAL\b", "BIGINT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBIGSERIAL\b", "BIGINT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bTRUE\b", "1", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bFALSE\b", "0", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bON\s+CONFLICT\s+DO\s+NOTHING\b", string.Empty, RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"INSERT\s+INTO\b", "INSERT IGNORE INTO", RegexOptions.IgnoreCase);
        return sql.Trim();
    }
}
