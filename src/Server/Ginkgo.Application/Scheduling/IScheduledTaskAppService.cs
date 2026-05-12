// 文件功能说明：
// 定时任务应用服务接口定义。

using Ginkgo.Shared;

namespace Ginkgo.Application.Scheduling;

/// <summary>
/// 定时任务应用服务接口。
/// </summary>
public interface IScheduledTaskAppService
{
    /// <summary>
    /// 获取所有任务列表。
    /// </summary>
    Task<List<ScheduledTaskItemDto>> GetAllTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取单个任务详情。
    /// </summary>
    Task<ScheduledTaskItemDto?> GetTaskByKeyAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 更新任务配置（启禁用/Cron/描述）。
    /// </summary>
    Task UpdateTaskAsync(string taskKey, UpdateScheduledTaskInput input, CancellationToken ct = default);

    /// <summary>
    /// 手动触发任务执行。
    /// </summary>
    Task TriggerTaskAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 分页查询指定任务的执行日志。
    /// </summary>
    Task<PagedResult<ScheduledTaskLogItemDto>> GetTaskLogsAsync(string taskKey, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 创建动态任务。
    /// </summary>
    Task<ScheduledTaskItemDto> CreateDynamicTaskAsync(CreateDynamicTaskInput input, CancellationToken ct = default);

    /// <summary>
    /// 删除动态任务。
    /// </summary>
    Task<bool> DeleteDynamicTaskAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 获取所有执行提供器列表（前端新增弹窗使用）。
    /// </summary>
    List<ExecutionProviderDto> GetExecutionProviders();

    /// <summary>
    /// 获取所有可调用动作列表（前端能力选择器使用）。
    /// </summary>
    List<InvocableActionDto> GetInvocableActions();

    /// <summary>
    /// 测试执行（不保存任务，仅验证并试运行）。
    /// </summary>
    Task<ActionExecutionResultDto> TestExecuteAsync(string executionSource, string configJson, CancellationToken ct = default);
}
