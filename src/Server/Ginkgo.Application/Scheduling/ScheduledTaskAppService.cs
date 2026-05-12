// 文件功能说明：
// 定时任务应用服务实现。

using Ginkgo.Domain.Scheduling;
using Ginkgo.Infrastructure.Scheduling;
using Ginkgo.Plugin.Abstractions;
using Ginkgo.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Application.Scheduling;

/// <summary>
/// 定时任务应用服务实现。
/// </summary>
public sealed class ScheduledTaskAppService : IScheduledTaskAppService
{
    private readonly IScheduledTaskRepository _repo;
    private readonly TaskSchedulerService _scheduler;
    private readonly ActionRegistry _actionRegistry;
    private readonly ExecutionProviderRegistry _providerRegistry;
    private readonly IServiceProvider _services;
    private readonly ILogger<ScheduledTaskAppService> _logger;

    public ScheduledTaskAppService(
        IScheduledTaskRepository repo,
        TaskSchedulerService scheduler,
        ActionRegistry actionRegistry,
        ExecutionProviderRegistry providerRegistry,
        IServiceProvider services,
        ILogger<ScheduledTaskAppService> logger)
    {
        _repo = repo;
        _scheduler = scheduler;
        _actionRegistry = actionRegistry;
        _providerRegistry = providerRegistry;
        _services = services;
        _logger = logger;
    }

    public async Task<List<ScheduledTaskItemDto>> GetAllTasksAsync(CancellationToken ct = default)
    {
        var tasks = await _repo.GetAllTasksAsync(ct);
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<ScheduledTaskItemDto?> GetTaskByKeyAsync(string taskKey, CancellationToken ct = default)
    {
        var task = await _repo.GetByKeyAsync(taskKey, ct);
        return task == null ? null : MapToDto(task);
    }

    public async Task UpdateTaskAsync(string taskKey, UpdateScheduledTaskInput input, CancellationToken ct = default)
    {
        var existing = await _repo.GetByKeyAsync(taskKey, ct)
            ?? throw new InvalidOperationException($"任务 {taskKey} 不存在");

        await _repo.UpdateConfigAsync(taskKey, input.IsEnabled, input.CronExpression, input.Description, ct);

        // 重新计算 NextRunAt 并持久化
        DateTime? nextRun = null;
        if (input.IsEnabled)
        {
            try
            {
                var cron = Cronos.CronExpression.Parse(input.CronExpression);
                var next = cron.GetNextOccurrence(DateTime.UtcNow);
                nextRun = next?.ToLocalTime();
            }
            catch { /* Cron 表达式无效时保持 null */ }
        }
        await _repo.UpdateRunInfoAsync(taskKey, existing.LastRunAt, nextRun, existing.LastResult, existing.LastElapsedMs, ct);

        // 通知调度引擎重新加载
        _scheduler.NotifyTasksChanged();
    }

    public async Task TriggerTaskAsync(string taskKey, CancellationToken ct = default)
    {
        await _scheduler.TriggerManuallyAsync(taskKey, ct);
    }

    public async Task<PagedResult<ScheduledTaskLogItemDto>> GetTaskLogsAsync(string taskKey, int page, int pageSize, CancellationToken ct = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var (items, total) = await _repo.GetLogsPagedAsync(taskKey, page, pageSize, ct);
        return new PagedResult<ScheduledTaskLogItemDto>
        {
            Total = total,
            Items = items.Select(l => new ScheduledTaskLogItemDto
            {
                Id = l.Id,
                TaskKey = l.TaskKey,
                StartedAt = l.StartedAt,
                FinishedAt = l.FinishedAt,
                Success = l.Success,
                ErrorMessage = l.ErrorMessage,
                ElapsedMs = l.ElapsedMs,
                TriggerType = l.TriggerType,
                DetailsJson = l.DetailsJson
            }).ToList()
        };
    }

    public async Task<ScheduledTaskItemDto> CreateDynamicTaskAsync(CreateDynamicTaskInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new InvalidOperationException("任务名称不能为空");
        if (string.IsNullOrWhiteSpace(input.CronExpression))
            throw new InvalidOperationException("Cron 表达式不能为空");
        if (string.IsNullOrWhiteSpace(input.ExecutionSource))
            throw new InvalidOperationException("执行源不能为空");

        var provider = _providerRegistry.Get(input.ExecutionSource)
            ?? throw new InvalidOperationException($"执行提供器 [{input.ExecutionSource}] 未注册");

        // 校验配置
        var validation = await provider.ValidateAsync(input.ConfigJson, _services);
        if (!validation.IsValid)
            throw new InvalidOperationException($"配置校验失败: {validation.ErrorMessage}");

        // 生成 TaskKey
        var taskKey = $"Dynamic.{input.ExecutionSource}.{Guid.NewGuid().ToString("N")[..8]}";

        var record = new ScheduledTaskRecord
        {
            TaskKey = taskKey,
            DisplayName = input.DisplayName,
            Group = input.Group,
            CronExpression = input.CronExpression,
            IsEnabled = input.IsEnabled,
            Description = input.Description,
            DefinitionType = "Dynamic",
            ExecutionSource = input.ExecutionSource,
            ExecutionType = provider.DisplayName,
            ExecutionTarget = input.DisplayName,
            ConfigJson = input.ConfigJson,
            Source = "手动创建"
        };

        await _repo.CreateDynamicTaskAsync(record, ct);

        // 计算 NextRunAt 并持久化
        if (record.IsEnabled)
        {
            try
            {
                var cron = Cronos.CronExpression.Parse(record.CronExpression);
                var next = cron.GetNextOccurrence(DateTime.UtcNow);
                var nextRun = next?.ToLocalTime();
                if (nextRun.HasValue)
                    await _repo.UpdateRunInfoAsync(taskKey, null, nextRun, null, null, ct);
            }
            catch { /* Cron 无效 */ }
        }

        // 通知调度引擎重新加载
        _scheduler.NotifyTasksChanged();

        var created = await _repo.GetByKeyAsync(taskKey, ct);
        return MapToDto(created ?? record);
    }

    public async Task<bool> DeleteDynamicTaskAsync(string taskKey, CancellationToken ct = default)
    {
        var result = await _repo.DeleteDynamicTaskAsync(taskKey, ct);
        if (result)
            _scheduler.NotifyTasksChanged();
        return result;
    }

    public List<ExecutionProviderDto> GetExecutionProviders()
    {
        return _providerRegistry.GetAll().Select(rp => new ExecutionProviderDto
        {
            SourceKey = rp.Provider.SourceKey,
            DisplayName = rp.Provider.DisplayName,
            Icon = rp.Provider.Icon,
            Description = rp.Provider.Description,
            Order = rp.Provider.Order,
            SupportsTest = rp.Provider.SupportsTest,
            FormDefinition = rp.Provider.GetFormDefinition()
        }).ToList();
    }

    public List<InvocableActionDto> GetInvocableActions()
    {
        return _actionRegistry.GetAll().Select(ra => new InvocableActionDto
        {
            ActionKey = ra.Action.ActionKey,
            DisplayName = ra.Action.DisplayName,
            Category = ra.Action.Category,
            Description = ra.Action.Description,
            Source = ra.SourceDisplayName ?? ra.Source,
            Parameters = ra.Action.Parameters?.Cast<object>().ToArray()
        }).ToList();
    }

    public async Task<ActionExecutionResultDto> TestExecuteAsync(string executionSource, string configJson, CancellationToken ct = default)
    {
        var provider = _providerRegistry.Get(executionSource)
            ?? throw new InvalidOperationException($"执行提供器 [{executionSource}] 未注册");

        if (!provider.SupportsTest)
            throw new InvalidOperationException($"执行提供器 [{executionSource}] 不支持测试执行");

        using var scope = _services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger($"TaskTest.{executionSource}");
        var context = new ActionContext(scope.ServiceProvider, ct, logger);

        var result = await provider.TestAsync(configJson, context);
        return new ActionExecutionResultDto
        {
            Success = result.Success,
            Message = result.Message,
            Data = result.Data
        };
    }

    private static ScheduledTaskItemDto MapToDto(ScheduledTaskRecord r) => new()
    {
        Id = r.Id,
        TaskKey = r.TaskKey,
        DisplayName = r.DisplayName,
        Group = r.Group,
        CronExpression = r.CronExpression,
        IsEnabled = r.IsEnabled,
        LastRunAt = r.LastRunAt,
        NextRunAt = r.NextRunAt,
        LastResult = r.LastResult,
        LastElapsedMs = r.LastElapsedMs,
        Description = r.Description,
        ExecutionType = r.ExecutionType,
        ExecutionTarget = r.ExecutionTarget,
        DefinitionType = r.DefinitionType,
        ExecutionSource = r.ExecutionSource,
        ActionKey = r.ActionKey,
        ConfigJson = r.ConfigJson,
        Source = r.Source,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
