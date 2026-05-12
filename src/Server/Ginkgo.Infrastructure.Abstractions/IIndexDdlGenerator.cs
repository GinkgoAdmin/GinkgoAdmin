// 文件功能说明：
// 索引 DDL 生成器接口。根据 IndexDefinition 和当前数据库方言，生成 CREATE INDEX DDL 语句。
// 用于安装脚本、迁移工具、开发期代码生成等场景。
//
// 各方言差异由 IDatabaseDialect 提供的标识符引用 + 能力检查承担：
// - MySQL：支持前缀索引 (col(64))、FULLTEXT INDEX
// - PostgreSQL：不支持前缀索引；全文索引用 GIN(to_tsvector('language', col))
// - SQL Server：不支持前缀索引；全文索引依赖 FULLTEXT CATALOG（本生成器仅输出 CREATE FULLTEXT INDEX 骨架）

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 索引 DDL 生成器。按方言输出 <c>CREATE INDEX</c> / <c>CREATE FULLTEXT INDEX</c> 等 DDL。
/// </summary>
public interface IIndexDdlGenerator
{
    /// <summary>
    /// 根据索引定义生成 CREATE INDEX DDL（不含末尾分号）。
    /// </summary>
    /// <param name="definition">索引定义。</param>
    /// <returns>方言特定的 CREATE INDEX DDL 语句。</returns>
    /// <exception cref="ArgumentException">定义不合法（缺表名/列名等）。</exception>
    /// <exception cref="NotSupportedException">当前方言不支持请求的索引类型（如 PG 的前缀索引）。</exception>
    string GenerateCreateIndex(IndexDefinition definition);

    /// <summary>当前方言是否支持前缀索引。</summary>
    bool SupportsPrefixIndex { get; }

    /// <summary>当前方言是否支持全文索引。</summary>
    bool SupportsFullTextIndex { get; }
}
