// 文件功能说明：
// MySQL 安装脚本 → PostgreSQL 的轻量 DDL/DML 转写器。
// 供 PostgreSqlDialect.TranslateMySqlDDL 在插件仅提供 MySQL 版 install.sql 时复用。

using System.Text;
using System.Text.RegularExpressions;

namespace Ginkgo.Infrastructure.SqlTranslation;

/// <summary>
/// MySQL 5.7 常见 DDL/DML 到 PostgreSQL 14+ 的机械化转写（仅覆盖插件 install.sql 常见模式）。
/// </summary>
public static class MySqlToPostgreSqlTranslator
{
    private static readonly Regex SessionPragmaRegex = new(
        @"^SET\s+(NAMES|FOREIGN_KEY_CHECKS|CHARACTER\s+SET|COLLATION|SQL_MODE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InlineKeyRegex = new(
        @",\s*KEY\s+`([^`]+)`\s*\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InlineKeyQuotedRegex = new(
        @",\s*KEY\s+""([^""]+)""\s*\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>尝试将单条 MySQL SQL 批次转写为 PostgreSQL 语法。</summary>
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

        var tableName = ExtractCreateTableName(sql);
        var indexStatements = new List<string>();
        var quotedTable = tableName != null ? $"\"{tableName}\"" : null;

        sql = Regex.Replace(sql, @"UNIQUE\s+KEY\s+`[^`]+`\s*\(", "UNIQUE (", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"UNIQUE\s+KEY\s+""[^""]+""\s*\(", "UNIQUE (", RegexOptions.IgnoreCase);

        if (quotedTable != null)
        {
            sql = ExtractInlineKeys(sql, InlineKeyRegex, quotedTable, indexStatements);
            sql = ExtractInlineKeys(sql, InlineKeyQuotedRegex, quotedTable, indexStatements);
        }

        sql = ApplyCommonReplacements(sql);
        sql = Regex.Replace(sql, @"\)\s*ENGINE\s*=\s*InnoDB[^;]*", ")", RegexOptions.IgnoreCase);

        if (indexStatements.Count == 0) return sql;

        var sb = new StringBuilder(sql.TrimEnd());
        if (!sb.ToString().EndsWith(';')) sb.Append(';');
        sb.AppendLine();
        foreach (var idx in indexStatements)
            sb.AppendLine(idx);
        return sb.ToString().TrimEnd();
    }

    private static string ExtractInlineKeys(
        string sql,
        Regex pattern,
        string quotedTable,
        List<string> indexStatements)
    {
        foreach (Match m in pattern.Matches(sql))
        {
            var idxName = m.Groups[1].Value;
            var cols = QuoteBacktickColumns(m.Groups[2].Value);
            indexStatements.Add($"CREATE INDEX IF NOT EXISTS \"{idxName}\" ON {quotedTable} ({cols});");
        }
        return pattern.Replace(sql, string.Empty);
    }

    private static string? ExtractCreateTableName(string sql)
    {
        var m = Regex.Match(sql,
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?`([^`]+)`",
            RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;

        m = Regex.Match(sql,
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?""([^""]+)""",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string StripColumnComments(string sql)
        => Regex.Replace(sql, @"\s+COMMENT\s+'(?:''|[^'])*'", string.Empty, RegexOptions.IgnoreCase);

    private static string QuoteBacktickColumns(string cols)
        => Regex.Replace(cols, "`([^`]+)`", "\"$1\"");

    private static string ApplyCommonReplacements(string sql)
    {
        sql = Regex.Replace(sql, "`([^`]+)`", "\"$1\"");
        sql = Regex.Replace(sql, @"\bTINYINT\s*\(\s*1\s*\)", "BOOLEAN", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bTINYINT\b", "SMALLINT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME\s*\(\s*6\s*\)", "TIMESTAMP(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME\b", "TIMESTAMP(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bLONGTEXT\b", "TEXT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bMEDIUMTEXT\b", "TEXT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bJSON\b", "JSONB", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bINT\b", "INTEGER", RegexOptions.IgnoreCase);
        // 已是 PG 原生 DOUBLE PRECISION 时不再二次替换，避免生成 "DOUBLE PRECISION PRECISION"
        sql = Regex.Replace(sql, @"\bDOUBLE\b(?!\s+PRECISION\b)", "DOUBLE PRECISION", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bUNSIGNED\b", string.Empty, RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\s+ON\s+UPDATE\s+CURRENT_TIMESTAMP(?:\(\d+\))?", string.Empty, RegexOptions.IgnoreCase);
        return sql.Trim();
    }
}
