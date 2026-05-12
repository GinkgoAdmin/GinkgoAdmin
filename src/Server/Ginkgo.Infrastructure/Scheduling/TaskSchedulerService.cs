// 文件功能说明：
// 定时任务调度引擎（BackgroundService），负责周期性检查到期任务并执行。

using Cronos;
using Ginkgo.Domain.Scheduling;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Scheduling;

/// <summary>
/// 定时任务调度引擎。
/// 启动时将注册表中的任务同步到数据库，之后每 15 秒扫描一次到期任务并执行。
/// </summary>
public sealed class TaskSchedulerService : BackgroundService
{
    private readonly ScheduledTaskRegistry _registry;
    private readonly ExecutionProviderRegistry _providerRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskSchedulerService> _logger;

    /// <summary>
    /// 调度循环间隔（秒）。
    /// </summary>
    private const int TickIntervalSeconds = 15;

    /// <summary>
    /// 任务变更信号（新增/删除动态任务时触发，让调度循环立即重新加载）。
    /// </summary>
    private volatile bool _tasksChanged;

    public TaskSchedulerService(
        ScheduledTaskRegistry registry,
        ExecutionProviderRegistry providerRegistry,
        IServiceScopeFactory scopeFactory,
        ILogger<TaskSchedulerService> logger)
    {
        _registry = registry;
        _providerRegistry = providerRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 通知调度引擎任务列表已变更（新增/删除动态任务后调用）。
    /// </summary>
    public void NotifyTasksChanged()
    {
        _tasksChanged = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[调度引擎] 启动，等待应用初始化完成...");
        // 等待 5 秒，确保数据库和模块全部就绪
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // 同步注册表到数据库
        await SyncRegistryToDbAsync(stoppingToken);

        _logger.LogInformation("[调度引擎] 开始调度循环，间隔 {Interval} 秒", TickIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 如果任务列表变更，重新计算 NextRunAt
                if (_tasksChanged)
                {
                    _tasksChanged = false;
                    await RefreshDynamicTaskSchedulesAsync(stoppingToken);
                }

                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[调度引擎] 调度循环异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(TickIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("[调度引擎] 已停止");
    }

    /// <summary>
    /// 刷新动态任务的 NextRunAt。
    /// </summary>
    private async Task RefreshDynamicTaskSchedulesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
            var allTasks = await repo.GetAllTasksAsync(ct);
            foreach (var task in allTasks.Where(t => t.IsEnabled && t.DefinitionType == "Dynamic"))
            {
                var nextRun = CalculateNextRun(task.CronExpression);
                if (nextRun.HasValue && task.NextRunAt != nextRun)
                    await repo.UpdateRunInfoAsync(task.TaskKey, task.LastRunAt, nextRun, task.LastResult, task.LastElapsedMs, ct);
            }
            _logger.LogInformation("[调度引擎] 已刷新动态任务调度计划");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[调度引擎] 刷新动态任务调度失败");
        }
    }

    /// <summary>
    /// 将内存注册表中的任务同步到数据库（Upsert）。
    /// </summary>
    private async Task SyncRegistryToDbAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();

            foreach (var registered in _registry.GetAll())
            {
                var task = registered.Task;
                var record = new ScheduledTaskRecord
                {
                    TaskKey = task.TaskKey,
                    DisplayName = task.DisplayName,
                    Group = string.IsNullOrWhiteSpace(registered.GroupName) ? task.Group : registered.GroupName,
                    CronExpression = task.CronExpression,
                    Description = task.Description,
                    ExecutionType = task.ExecutionType,
                    ExecutionTarget = task.ExecutionTarget,
                    Source = string.IsNullOrWhiteSpace(registered.SourceDisplayName) ? registered.Source : registered.SourceDisplayName,
                    IsEnabled = true
                };
                await repo.UpsertAsync(record, ct);
            }

            // 计算所有已启用任务的 NextRunAt
            var allTasks = await repo.GetAllTasksAsync(ct);
            foreach (var dbTask in allTasks.Where(t => t.IsEnabled))
            {
                var nextRun = CalculateNextRun(dbTask.CronExpression);
                if (nextRun.HasValue)
                {
                    await repo.UpdateRunInfoAsync(dbTask.TaskKey, dbTask.LastRunAt, nextRun, dbTask.LastResult, dbTask.LastElapsedMs, ct);
                }
            }

            _logger.LogInformation("[调度引擎] 已同步 {Count} 个任务到数据库", _registry.GetAll().Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[调度引擎] 同步注册表到数据库失败");
        }
    }

    /// <summary>
    /// 单次调度检查：扫描到期任务并执行。
    /// </summary>
    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
        var allTasks = await repo.GetAllTasksAsync(ct);
        var now = DateTime.Now;

        // 兜底：对已启用但 NextRunAt 为 NULL 的任务自动补算调度时间
        foreach (var orphan in allTasks.Where(t => t.IsEnabled && !t.NextRunAt.HasValue))
        {
            var nextRun = CalculateNextRun(orphan.CronExpression);
            if (nextRun.HasValue)
            {
                _logger.LogInformation("[调度引擎] 自动补算任务 {TaskKey} 的 NextRunAt = {NextRun}", orphan.TaskKey, nextRun);
                await repo.UpdateRunInfoAsync(orphan.TaskKey, orphan.LastRunAt, nextRun, orphan.LastResult, orphan.LastElapsedMs, ct);
                orphan.NextRunAt = nextRun; // 刷新内存中的值，本轮可能就该执行
            }
        }

        foreach (var dbTask in allTasks.Where(t => t.IsEnabled && t.NextRunAt.HasValue && t.NextRunAt.Value <= now))
        {
            // 防止并发：先更新 NextRunAt 为下一个周期
            var nextRun = CalculateNextRun(dbTask.CronExpression);
            await repo.UpdateRunInfoAsync(dbTask.TaskKey, now, nextRun, "Running", null, ct);

            if (dbTask.DefinitionType == "Dynamic" && !string.IsNullOrWhiteSpace(dbTask.ExecutionSource))
            {
                // 动态任务：通过执行提供器执行
                _ = ExecuteDynamicTaskAsync(dbTask, "Auto", ct);
            }
            else
            {
                // 代码注册任务：通过 IScheduledTask 执行
                var registered = _registry.Get(dbTask.TaskKey);
                if (registered == null)
                {
                    _logger.LogWarning("[调度引擎] 任务 {TaskKey} 在注册表中未找到，跳过执行", dbTask.TaskKey);
                    continue;
                }
                _ = ExecuteCodeBasedTaskAsync(registered, dbTask.TaskKey, dbTask.CronExpression, ct);
            }
        }
    }

    /// <summary>
    /// 执行代码注册型任务（IScheduledTask）。
    /// </summary>
    private async Task ExecuteCodeBasedTaskAsync(ScheduledTaskRegistry.RegisteredTask registered, string taskKey, string cronExpression, CancellationToken appCt)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var log = new ScheduledTaskLog
        {
            TaskKey = taskKey,
            StartedAt = DateTime.Now,
            TriggerType = "Auto"
        };

        ScheduledTaskContext? context = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
            var taskLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"ScheduledTask.{taskKey}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(appCt);
            cts.CancelAfter(TimeSpan.FromMinutes(30));

            context = new ScheduledTaskContext(scope.ServiceProvider, cts.Token, taskLogger);
            await registered.Task.ExecuteAsync(context);

            sw.Stop();
            log.Success = true;
            log.FinishedAt = DateTime.Now;
            log.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.DetailsJson = SerializeOutput(context, success: true);

            await repo.AddLogAsync(log, appCt);
            await repo.UpdateRunInfoAsync(taskKey, log.StartedAt, CalculateNextRun(cronExpression), "Success", log.ElapsedMs, appCt);

            _logger.LogInformation("[调度引擎] 任务 {TaskKey} 执行成功，耗时 {Elapsed}ms", taskKey, log.ElapsedMs);
        }
        catch (Exception ex)
        {
            sw.Stop();
            log.Success = false;
            log.FinishedAt = DateTime.Now;
            log.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            log.DetailsJson = SerializeOutput(context, success: false, exception: ex);

            _logger.LogError(ex, "[调度引擎] 任务 {TaskKey} 执行失败", taskKey);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
                await repo.AddLogAsync(log, appCt);
                await repo.UpdateRunInfoAsync(taskKey, log.StartedAt, CalculateNextRun(cronExpression), "Failed", log.ElapsedMs, appCt);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "[调度引擎] 记录任务 {TaskKey} 执行日志失败", taskKey);
            }
        }
    }

    /// <summary>
    /// 执行动态配置型任务（通过 ITaskExecutionProvider 分发）。
    /// </summary>
    private async Task ExecuteDynamicTaskAsync(ScheduledTaskRecord dbTask, string triggerType, CancellationToken appCt)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var log = new ScheduledTaskLog
        {
            TaskKey = dbTask.TaskKey,
            StartedAt = DateTime.Now,
            TriggerType = triggerType
        };

        try
        {
            var provider = _providerRegistry.Get(dbTask.ExecutionSource!)
                ?? throw new InvalidOperationException($"执行提供器 [{dbTask.ExecutionSource}] 未注册");

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
            var taskLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"DynamicTask.{dbTask.TaskKey}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(appCt);
            cts.CancelAfter(TimeSpan.FromMinutes(30));

            var context = new ActionContext(scope.ServiceProvider, cts.Token, taskLogger);
            var result = await provider.ExecuteAsync(dbTask.ConfigJson ?? "{}", context);

            sw.Stop();
            log.Success = result.Success;
            log.FinishedAt = DateTime.Now;
            log.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.ErrorMessage = result.Success ? null : result.Message;
            log.DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                executionSource = dbTask.ExecutionSource,
                result.Success,
                result.Message,
                result.Data
            });

            await repo.AddLogAsync(log, appCt);
            var status = result.Success ? "Success" : "Failed";
            await repo.UpdateRunInfoAsync(dbTask.TaskKey, log.StartedAt, CalculateNextRun(dbTask.CronExpression), status, log.ElapsedMs, appCt);

            if (result.Success)
                _logger.LogInformation("[调度引擎] 动态任务 {TaskKey} 执行成功，耗时 {Elapsed}ms: {Message}", dbTask.TaskKey, log.ElapsedMs, result.Message);
            else
                _logger.LogWarning("[调度引擎] 动态任务 {TaskKey} 执行失败: {Message}", dbTask.TaskKey, result.Message);
        }
        catch (Exception ex)
        {
            sw.Stop();
            log.Success = false;
            log.FinishedAt = DateTime.Now;
            log.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            _logger.LogError(ex, "[调度引擎] 动态任务 {TaskKey} 执行异常", dbTask.TaskKey);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
                await repo.AddLogAsync(log, appCt);
                await repo.UpdateRunInfoAsync(dbTask.TaskKey, log.StartedAt, CalculateNextRun(dbTask.CronExpression), "Failed", log.ElapsedMs, appCt);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "[调度引擎] 记录动态任务 {TaskKey} 执行日志失败", dbTask.TaskKey);
            }
        }
    }

    /// <summary>
    /// 手动触发任务（供应用服务调用）。
    /// </summary>
    public async Task TriggerManuallyAsync(string taskKey, CancellationToken ct = default)
    {
        // 先查数据库确定任务类型
        using var lookupScope = _scopeFactory.CreateScope();
        var lookupRepo = lookupScope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
        var dbTask = await lookupRepo.GetByKeyAsync(taskKey, ct);

        if (dbTask != null && dbTask.DefinitionType == "Dynamic" && !string.IsNullOrWhiteSpace(dbTask.ExecutionSource))
        {
            // 动态任务：通过 Provider 执行
            await ExecuteDynamicTaskAsync(dbTask, "Manual", ct);
            return;
        }

        // 代码注册任务
        var registered = _registry.Get(taskKey)
            ?? throw new InvalidOperationException($"任务 {taskKey} 未在注册表中找到");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var log = new ScheduledTaskLog
        {
            TaskKey = taskKey,
            StartedAt = DateTime.Now,
            TriggerType = "Manual"
        };

        ScheduledTaskContext? context = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
            var taskLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"ScheduledTask.{taskKey}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(30));

            context = new ScheduledTaskContext(scope.ServiceProvider, cts.Token, taskLogger);
            await registered.Task.ExecuteAsync(context);

            sw.Stop();
            log.Success = true;
            log.FinishedAt = DateTime.Now;
            log.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.DetailsJson = SerializeOutput(context, success: true);

            await repo.AddLogAsync(log, ct);
            await repo.UpdateRunInfoAsync(taskKey, log.StartedAt, null, "Success", log.ElapsedMs, ct);

            _logger.LogInformation("[调度引擎] 手动触发任务 {TaskKey} 执行成功，耗时 {Elapsed}ms", taskKey, log.ElapsedMs);
        }
        catch (Exception ex)
        {
            sw.Stop();
            log.Success = false;
            log.FinishedAt = DateTime.Now;
            log.ElapsedMs = (int)sw.ElapsedMilliseconds;
            log.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            log.DetailsJson = SerializeOutput(context, success: false, exception: ex);

            _logger.LogError(ex, "[调度引擎] 手动触发任务 {TaskKey} 执行失败", taskKey);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
                await repo.AddLogAsync(log, ct);
                await repo.UpdateRunInfoAsync(taskKey, log.StartedAt, null, "Failed", log.ElapsedMs, ct);
            }
            catch { }

            throw;
        }
    }

    /// <summary>
    /// 将 ScheduledTaskContext 的输出缓冲序列化为 DetailsJson。
    /// 输出结构：{ success, exception?, output: [{ level, at, message }] }
    /// </summary>
    private static string? SerializeOutput(ScheduledTaskContext? context, bool success, Exception? exception = null)
    {
        if (context == null && exception == null) return null;

        try
        {
            var payload = new
            {
                success,
                exception = exception == null ? null : new
                {
                    type = exception.GetType().FullName,
                    message = exception.Message,
                    stackTrace = exception.StackTrace
                },
                output = context?.Output.Select(o => new
                {
                    level = o.Level,
                    at = o.At.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    message = o.Message
                }).ToArray() ?? Array.Empty<object>()
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });
            // 限长避免过大
            return json.Length > 32_000 ? json[..32_000] + "...(truncated)" : json;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 使用 Cronos 计算下次执行时间。
    /// </summary>
    private static DateTime? CalculateNextRun(string cronExpression)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression);
            var next = cron.GetNextOccurrence(DateTime.UtcNow);
            // 转换为本地时间（中国时区 UTC+8）
            return next?.ToLocalTime();
        }
        catch
        {
            return null;
        }
    }
}
