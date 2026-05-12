// 文件功能说明：
// 大数据写入抽象。所有模块/插件需要批量插入或更新时，统一通过 IBulkInsertService 调用，
// 由 Ginkgo.Infrastructure 提供默认实现（包装 SqlSugar 的 Fastest<T>().BulkCopy / BulkUpdate）。
//
// 设计要点：
// - 通过 db.json 的 Database.Features.BulkOps 开关控制；Enabled=false 时降级为逐行 Insertable，保证降级路径可用。
// - 不直接暴露 SqlSugar API，模块/插件无需引用 SqlSugarCore 即可使用大数据写入能力。
// - 业务侧（如 ImportController）只关心"我要写入这批数据"，不关心方言、批次大小、内部是 BulkCopy 还是 Insertable。

using System.Data;

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 批量数据写入服务（基于 SqlSugar Fastest&lt;T&gt;）。
/// </summary>
public interface IBulkInsertService
{
    /// <summary>
    /// 批量插入实体。<c>Database.Features.BulkOps.Enabled=true</c> 时走 BulkCopy，否则降级为 Insertable 逐行插入。
    /// </summary>
    /// <typeparam name="T">实体类型，需具备无参构造函数。</typeparam>
    /// <param name="entities">待插入实体列表；空列表直接返回 0。</param>
    /// <param name="batchSize">批量大小（可选）；不指定时使用 <c>BulkOpsOptions.DefaultBatchSize</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>实际写入行数。</returns>
    Task<int> BulkInsertAsync<T>(
        IList<T> entities,
        int? batchSize = null,
        CancellationToken ct = default) where T : class, new();

    /// <summary>
    /// 通过 DataTable 批量插入到指定表名（用于动态字段、字典型导入场景）。
    /// </summary>
    /// <param name="tableName">目标表名（已通过白名单校验）。</param>
    /// <param name="data">数据表，列名必须与表列名匹配（区分大小写视方言）。</param>
    /// <param name="batchSize">批量大小（可选）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>实际写入行数。</returns>
    Task<int> BulkInsertDataTableAsync(
        string tableName,
        DataTable data,
        int? batchSize = null,
        CancellationToken ct = default);

    /// <summary>
    /// 批量更新实体（按主键）。<c>Enabled=false</c> 时降级为逐行 Updateable。
    /// </summary>
    Task<int> BulkUpdateAsync<T>(
        IList<T> entities,
        int? batchSize = null,
        CancellationToken ct = default) where T : class, new();
}
