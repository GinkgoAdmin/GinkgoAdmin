// 文件功能说明：
// 定义定时任务仓储接口，包含任务记录与执行日志的数据访问操作。

namespace Ginkgo.Domain.Scheduling;

/// <summary>
/// 定时任务仓储接口。
/// </summary>
public interface IScheduledTaskRepository
{
    // ===== 任务记录 =====

    /// <summary>
    /// 获取所有任务记录。
    /// </summary>
    Task<List<ScheduledTaskRecord>> GetAllTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// 按 TaskKey 获取任务记录。
    /// </summary>
    Task<ScheduledTaskRecord?> GetByKeyAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 插入或更新任务记录（按 TaskKey 判断是否存在）。
    /// 如果已存在，仅更新 DisplayName/Group/Description/Source，不覆盖用户自定义的 CronExpression/IsEnabled。
    /// </summary>
    Task UpsertAsync(ScheduledTaskRecord record, CancellationToken ct = default);

    /// <summary>
    /// 更新任务的运行信息（LastRunAt/NextRunAt/LastResult/LastElapsedMs）。
    /// </summary>
    Task UpdateRunInfoAsync(string taskKey, DateTime? lastRunAt, DateTime? nextRunAt, string? lastResult, int? lastElapsedMs, CancellationToken ct = default);

    /// <summary>
    /// 更新任务的可配置项（IsEnabled/CronExpression/Description）。
    /// </summary>
    Task UpdateConfigAsync(string taskKey, bool isEnabled, string cronExpression, string? description, CancellationToken ct = default);

    /// <summary>
    /// 创建动态任务（完整插入，不走 Upsert 的元数据保护逻辑）。
    /// </summary>
    Task CreateDynamicTaskAsync(ScheduledTaskRecord record, CancellationToken ct = default);

    /// <summary>
    /// 删除动态任务（仅允许删除 DefinitionType=Dynamic 的任务）。
    /// </summary>
    Task<bool> DeleteDynamicTaskAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 更新动态任务的完整配置。
    /// </summary>
    Task UpdateDynamicTaskAsync(ScheduledTaskRecord record, CancellationToken ct = default);

    // ===== 执行日志 =====

    /// <summary>
    /// 添加执行日志。
    /// </summary>
    Task AddLogAsync(ScheduledTaskLog log, CancellationToken ct = default);

    /// <summary>
    /// 分页查询指定任务的执行日志（按 StartedAt 倒序）。
    /// </summary>
    Task<(List<ScheduledTaskLog> Items, int Total)> GetLogsPagedAsync(string taskKey, int page, int pageSize, CancellationToken ct = default);
}
