// 跨数据库方言 SQL 脚本转写编排：按语句边界切分后逐条转写。

using System.Text;
using System.Text.RegularExpressions;

namespace Ginkgo.Infrastructure.SqlTranslation;

/// <summary>
/// 将完整 SQL 脚本从源方言转写为目标方言（按分号切分批次）。
/// </summary>
public static class ModuleSqlScriptTranslator
{
    /// <summary>将脚本从源方言转写为目标方言。</summary>
    public static string TranslateScript(string script, string sourceDialectCode, string targetDialectCode)
    {
        if (string.IsNullOrWhiteSpace(script)) return string.Empty;
        if (IsSameDialect(sourceDialectCode, targetDialectCode)) return script;

        var sb = new StringBuilder(script.Length + 256);
        foreach (var batch in SplitSqlBatches(script))
        {
            var trimmed = batch.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith("--", StringComparison.Ordinal)) continue;

            var translated = TranslateBatch(trimmed, sourceDialectCode, targetDialectCode);
            if (string.IsNullOrWhiteSpace(translated) ||
                translated.StartsWith("-- skipped", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.AppendLine(translated.TrimEnd(';') + ";");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>将单条 SQL 批次从源方言转写为目标方言。</summary>
    public static string TranslateBatch(string batch, string sourceDialectCode, string targetDialectCode)
    {
        if (string.IsNullOrWhiteSpace(batch)) return batch;
        if (IsSameDialect(sourceDialectCode, targetDialectCode)) return batch;

        var source = NormalizeDialectCode(sourceDialectCode);
        var target = NormalizeDialectCode(targetDialectCode);

        // 先归一到 MySQL 中间表示，再转到目标方言。
        var mysqlCanonical = source switch
        {
            "mysql" => batch,
            "postgresql" => PostgreSqlToMySqlTranslator.Translate(batch),
            "sqlserver" => batch, // SQL Server 导出已尽量贴近 MySQL 语法
            _ => batch
        };

        return target switch
        {
            "mysql" => mysqlCanonical,
            "postgresql" => MySqlToPostgreSqlTranslator.Translate(mysqlCanonical),
            "sqlserver" => MySqlToSqlServerTranslator.Translate(mysqlCanonical),
            _ => mysqlCanonical
        };
    }

    private static bool IsSameDialect(string? a, string? b)
        => string.Equals(NormalizeDialectCode(a), NormalizeDialectCode(b), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDialectCode(string? code)
        => (code ?? "mysql").Trim().ToLowerInvariant() switch
        {
            "pgsql" => "postgresql",
            "postgres" => "postgresql",
            "mssql" => "sqlserver",
            _ => (code ?? "mysql").Trim().ToLowerInvariant()
        };

    private static IEnumerable<string> SplitSqlBatches(string sql)
    {
        var sb = new StringBuilder();
        var inString = false;
        char quote = '\0';
        var escape = false;

        foreach (var c in sql)
        {
            if (inString)
            {
                sb.Append(c);
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == quote) { inString = false; quote = '\0'; }
                continue;
            }

            if (c is '\'' or '"')
            {
                inString = true;
                quote = c;
                sb.Append(c);
                continue;
            }

            if (c == ';')
            {
                yield return sb.ToString();
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }
}
