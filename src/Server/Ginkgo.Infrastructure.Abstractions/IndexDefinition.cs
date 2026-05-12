// 文件功能说明：
// 索引定义模型。用于描述需要在数据库中创建的索引，由 IIndexDdlGenerator 消费并生成方言特定的 CREATE INDEX DDL。
//
// 适用场景（详见 document/SqlSugar 性能优化建议.md §P2.5）：
// - 前缀索引（MySQL 专有）：大文本列的前 N 字符索引，避免全列索引占用过大。
// - 全文索引：MySQL FULLTEXT / PostgreSQL GIN(to_tsvector) / SQL Server FULLTEXT CATALOG。
// - 普通 B-tree 索引：通用多列复合索引。
// 不引入 db.json 开关（属于实体设计与 DDL/迁移脚本层面）。

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>索引类型。</summary>
public enum IndexType
{
    /// <summary>普通 B-tree 索引。</summary>
    BTree,

    /// <summary>全文索引（MySQL FULLTEXT / PG GIN tsvector / MSSQL FULLTEXT）。</summary>
    FullText,
}

/// <summary>索引中的单列定义。</summary>
public sealed class IndexColumnDefinition
{
    /// <summary>列名。</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>前缀长度（仅 MySQL 有效）。为 null 时不使用前缀索引。</summary>
    public int? PrefixLength { get; set; }

    /// <summary>排序方向（仅 B-tree）。true=DESC, false/null=ASC。</summary>
    public bool? Descending { get; set; }
}

/// <summary>
/// 索引定义。描述一个待生成的数据库索引，交由 <see cref="IIndexDdlGenerator"/> 按方言输出 DDL。
/// </summary>
public sealed class IndexDefinition
{
    /// <summary>索引名称（全局唯一推荐：<c>idx_{table}_{col1}[_{col2}...]</c>）。</summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>目标表 Schema（可选；MySQL 忽略、PG/MSSQL 按需填写）。</summary>
    public string? Schema { get; set; }

    /// <summary>目标表名。</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>索引类型（BTree / FullText）。</summary>
    public IndexType Type { get; set; } = IndexType.BTree;

    /// <summary>是否唯一索引（仅 B-tree 有效）。</summary>
    public bool IsUnique { get; set; }

    /// <summary>索引包含的列定义（有序）。</summary>
    public IReadOnlyList<IndexColumnDefinition> Columns { get; set; } = Array.Empty<IndexColumnDefinition>();

    /// <summary>全文索引使用的语言/分析器（PG: 'simple'/'english'/'chinese'；MySQL/MSSQL 可忽略或使用默认）。</summary>
    public string? FullTextLanguage { get; set; }
}
