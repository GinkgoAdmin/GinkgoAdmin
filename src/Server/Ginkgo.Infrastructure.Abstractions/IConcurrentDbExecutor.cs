// 文件功能说明：
// 并发数据库操作执行器抽象。当业务需要并发执行多个独立数据库查询时（如仪表盘并发拉 N 个统计指标），
// 通过 IConcurrentDbExecutor 统一调度，由框架负责实例隔离（CopyNew）与并发度限流。
//
// 设计要点：
// - 通过 db.json 的 Database.Features.Concurrency 开关控制；Enabled=false 时退化为串行执行，零额外开销。
// - Enabled=true 时每个操作获得独立的 ISqlSugarClient 实例（CopyNew），避免线程安全问题。
// - MaxDegreeOfParallelism 控制最大并行数，建议不超过连接池上限的 1/3。
// - 不直接暴露 SqlSugar CopyNew API，模块/插件只关心"给我并发跑这些查询"。

using SqlSugar;

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 并发数据库操作执行器。基于 SqlSugar <c>CopyNew()</c> 实现实例隔离 + 并发度限流。
/// <c>Database.Features.Concurrency.Enabled=false</c> 时退化为串行执行（共享同一 client）。
/// </summary>
public interface IConcurrentDbExecutor
{
    /// <summary>
    /// 并发执行多个有返回值的数据库操作。
    /// 每个操作接收独立的 <see cref="ISqlSugarClient"/> 实例（Enabled 时 CopyNew，Disabled 时共享）。
    /// </summary>
    /// <typeparam name="TResult">每个操作的返回值类型。</typeparam>
    /// <param name="operations">操作委托列表；每个委托接收 (ISqlSugarClient, CancellationToken)，返回 Task&lt;TResult&gt;。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>按传入顺序排列的结果列表。</returns>
    Task<IReadOnlyList<TResult>> RunAsync<TResult>(
        IReadOnlyList<Func<ISqlSugarClient, CancellationToken, Task<TResult>>> operations,
        CancellationToken ct = default);

    /// <summary>
    /// 并发执行多个无返回值的数据库操作。
    /// </summary>
    /// <param name="operations">操作委托列表。</param>
    /// <param name="ct">取消令牌。</param>
    Task RunAsync(
        IReadOnlyList<Func<ISqlSugarClient, CancellationToken, Task>> operations,
        CancellationToken ct = default);
}
