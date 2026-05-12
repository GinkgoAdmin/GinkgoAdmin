// 文件功能说明：
// 数据库方言"能力清单"。让上层业务在编写跨库代码时知道当前方言是否支持某项高级特性
// （如递归 CTE / 窗口函数 / JSON 函数 等），并据此选择实现路径（如递归 CTE 与 BFS 兜底）。

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 数据库方言能力清单。
/// <para>
/// 注意：这里只声明"是否支持"，不声明性能/语法差异。复杂方言差异请使用 IDatabaseDialect
/// 暴露的相关方法/属性（如 UtcNowExpr、SplitBatches、SqlGetRowVersionColumns 等）。
/// </para>
/// </summary>
/// <param name="SupportsRecursiveCte">是否支持递归 CTE (WITH RECURSIVE)。MySQL 5.7 不支持，MySQL 8+/SQL Server/PG 支持。</param>
/// <param name="SupportsWindowFunctions">是否支持窗口函数 (ROW_NUMBER OVER 等)。</param>
/// <param name="SupportsJsonFunctions">是否原生支持 JSON 列与 JSON 函数。</param>
/// <param name="SupportsMergeStatement">是否支持 MERGE 语句（SQL Server / Oracle / PG 15+）。</param>
/// <param name="SupportsArrayType">是否支持原生数组类型（PostgreSQL 支持）。</param>
/// <param name="SupportsFullTextSearch">是否原生支持全文检索（MATCH AGAINST / CONTAINS / tsvector）。</param>
/// <param name="SupportsMultipleActiveResultSets">是否需要/支持在连接字符串中开启 MultipleActiveResultSets（仅 SQL Server）。</param>
/// <param name="NeedsRowVersionInsertSanitization">是否需要 RowVersion/timestamp 列的特殊插入清洗（仅 SQL Server）。</param>
public sealed record DialectCapabilities(
    bool SupportsRecursiveCte,
    bool SupportsWindowFunctions,
    bool SupportsJsonFunctions,
    bool SupportsMergeStatement,
    bool SupportsArrayType,
    bool SupportsFullTextSearch,
    bool SupportsMultipleActiveResultSets,
    bool NeedsRowVersionInsertSanitization);
