// 文件功能说明：
// IIndexDdlGenerator 默认实现。基于 IDatabaseDialect 按方言生成 CREATE INDEX DDL：
// - MySQL：支持前缀索引 col(N)、FULLTEXT INDEX。
// - PostgreSQL：B-tree + GIN(to_tsvector(...)) 全文索引；前缀索引不支持。
// - SQL Server：B-tree + CREATE FULLTEXT INDEX 骨架；前缀索引不支持。
// 不引入 db.json 开关（属于实体设计与 DDL/迁移脚本层面）。

using Ginkgo.Infrastructure.Abstractions;
using System.Text;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 索引 DDL 生成器。注册为 Scoped，按当前请求的方言输出 DDL。
/// </summary>
public sealed class IndexDdlGenerator : IIndexDdlGenerator
{
    private readonly IDatabaseDialect _dialect;

    public IndexDdlGenerator(IDatabaseDialect dialect)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    }

    /// <inheritdoc />
    public bool SupportsPrefixIndex => _dialect.Code == "mysql";

    /// <inheritdoc />
    public bool SupportsFullTextIndex => _dialect.Capabilities.SupportsFullTextSearch;

    /// <inheritdoc />
    public string GenerateCreateIndex(IndexDefinition def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrWhiteSpace(def.TableName))
            throw new ArgumentException("TableName 不能为空。", nameof(def));
        if (string.IsNullOrWhiteSpace(def.IndexName))
            throw new ArgumentException("IndexName 不能为空。", nameof(def));
        if (def.Columns == null || def.Columns.Count == 0)
            throw new ArgumentException("Columns 至少需要一个列定义。", nameof(def));

        return def.Type switch
        {
            IndexType.BTree => GenerateBTreeIndex(def),
            IndexType.FullText => GenerateFullTextIndex(def),
            _ => throw new NotSupportedException($"不支持的索引类型：{def.Type}")
        };
    }

    /// <summary>生成 B-tree 索引 DDL。</summary>
    private string GenerateBTreeIndex(IndexDefinition def)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE ");
        if (def.IsUnique) sb.Append("UNIQUE ");
        sb.Append("INDEX ");
        sb.Append(_dialect.QuoteIdentifier(def.IndexName));
        sb.Append(" ON ");
        sb.Append(_dialect.QuoteTable(def.Schema, def.TableName));
        sb.Append(" (");

        for (var i = 0; i < def.Columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var col = def.Columns[i];
            sb.Append(_dialect.QuoteIdentifier(col.ColumnName));

            // 前缀索引（MySQL 专有）
            if (col.PrefixLength.HasValue && col.PrefixLength.Value > 0)
            {
                if (!SupportsPrefixIndex)
                    throw new NotSupportedException(
                        $"方言 {_dialect.DisplayName} 不支持前缀索引（列 {col.ColumnName} 设置了 PrefixLength={col.PrefixLength}）。");
                sb.Append($"({col.PrefixLength.Value})");
            }

            // 排序方向
            if (col.Descending == true)
                sb.Append(" DESC");
        }

        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>生成全文索引 DDL（按方言分支）。</summary>
    private string GenerateFullTextIndex(IndexDefinition def)
    {
        if (!SupportsFullTextIndex)
            throw new NotSupportedException($"方言 {_dialect.DisplayName} 不支持全文索引。");

        var code = _dialect.Code;

        if (code == "mysql")
            return GenerateMySqlFullText(def);

        if (code == "postgresql")
            return GeneratePgFullText(def);

        if (code == "sqlserver")
            return GenerateSqlServerFullText(def);

        // 未知方言兜底：尝试普通 B-tree
        return GenerateBTreeIndex(def);
    }

    /// <summary>MySQL: CREATE FULLTEXT INDEX idx ON tbl (col1, col2)</summary>
    private string GenerateMySqlFullText(IndexDefinition def)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE FULLTEXT INDEX ");
        sb.Append(_dialect.QuoteIdentifier(def.IndexName));
        sb.Append(" ON ");
        sb.Append(_dialect.QuoteTable(def.Schema, def.TableName));
        sb.Append(" (");
        AppendColumnNames(sb, def.Columns);
        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>PostgreSQL: CREATE INDEX idx ON tbl USING GIN(to_tsvector('lang', col1 || ' ' || col2))</summary>
    private string GeneratePgFullText(IndexDefinition def)
    {
        var lang = string.IsNullOrWhiteSpace(def.FullTextLanguage) ? "simple" : def.FullTextLanguage;
        var sb = new StringBuilder();
        sb.Append("CREATE INDEX ");
        sb.Append(_dialect.QuoteIdentifier(def.IndexName));
        sb.Append(" ON ");
        sb.Append(_dialect.QuoteTable(def.Schema, def.TableName));
        sb.Append(" USING GIN(to_tsvector('");
        sb.Append(lang);
        sb.Append("', ");

        // 多列用 || ' ' || 拼接
        for (var i = 0; i < def.Columns.Count; i++)
        {
            if (i > 0) sb.Append(" || ' ' || ");
            sb.Append(_dialect.QuoteIdentifier(def.Columns[i].ColumnName));
        }

        sb.Append("))");
        return sb.ToString();
    }

    /// <summary>
    /// SQL Server: CREATE FULLTEXT INDEX ON tbl (col1, col2) KEY INDEX pk_idx ON catalog_name
    /// 注意：SQL Server 全文索引需要先创建 FULLTEXT CATALOG；此处仅输出骨架 DDL，PrimaryKeyIndex 用占位符。
    /// </summary>
    private string GenerateSqlServerFullText(IndexDefinition def)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE FULLTEXT INDEX ON ");
        sb.Append(_dialect.QuoteTable(def.Schema, def.TableName));
        sb.Append(" (");
        AppendColumnNames(sb, def.Columns);
        sb.Append(") KEY INDEX [PK_");
        sb.Append(def.TableName);
        sb.Append("] ON [ft_catalog_default]");
        return sb.ToString();
    }

    /// <summary>拼接列名列表（不含前缀长度/排序）。</summary>
    private void AppendColumnNames(StringBuilder sb, IReadOnlyList<IndexColumnDefinition> cols)
    {
        for (var i = 0; i < cols.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_dialect.QuoteIdentifier(cols[i].ColumnName));
        }
    }
}
