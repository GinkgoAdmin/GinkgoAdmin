// 文件功能说明：
// 通用"分批迭代查询"抽象。用于定时任务 / 后台导出 / 大表汇总等场景，
// 避免 ToListAsync 一次性拉全表导致内存占用过高。
//
// 设计要点：
// - 不挂在 Database.Features 开关之下（这是基础编程能力、不存在"零开销 vs 有开销"之争）。
// - 接收 SqlSugar 的 ISugarQueryable<T>，调用方先用 _db.Queryable<T>().Where(...).OrderBy(...) 构造好查询，
//   然后交给本服务按页迭代。
// - 每页回调允许执行写库、调外部接口；回调内的异常会向上抛出，调用方决定吞并继续还是终止。
// - 当回调返回 false 时，迭代会提前停止（用于"找到目标就退出"场景）。

using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 通用分批迭代查询服务。
/// </summary>
public interface IIterativeQueryService
{
    /// <summary>
    /// 按 <paramref name="pageSize"/> 把 <paramref name="queryable"/> 的结果分页拉取，每页交给 <paramref name="pageHandler"/> 处理。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    /// <param name="queryable">已经组装好筛选 / 排序的查询对象（由调用方构造）。<br/>
    /// 建议至少调用 <c>OrderBy(...)</c>，否则不同页之间的顺序可能不稳定。</param>
    /// <param name="pageSize">每页大小；&lt;= 0 时按 1000 处理。</param>
    /// <param name="pageHandler">每页处理回调；参数为 <c>(items, pageIndex)</c>，<c>pageIndex</c> 从 1 开始。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>实际处理的总行数。</returns>
    Task<long> PageEachAsync<T>(
        ISugarQueryable<T> queryable,
        int pageSize,
        Func<IReadOnlyList<T>, int, Task> pageHandler,
        CancellationToken ct = default)
        where T : class, new();

    /// <summary>
    /// 同 <see cref="PageEachAsync{T}(ISugarQueryable{T}, int, Func{IReadOnlyList{T}, int, Task}, CancellationToken)"/>，
    /// 但回调可返回 <c>false</c> 来提前结束迭代（用于"找到即退出"场景）。
    /// </summary>
    Task<long> PageEachUntilAsync<T>(
        ISugarQueryable<T> queryable,
        int pageSize,
        Func<IReadOnlyList<T>, int, Task<bool>> pageHandler,
        CancellationToken ct = default)
        where T : class, new();
}
