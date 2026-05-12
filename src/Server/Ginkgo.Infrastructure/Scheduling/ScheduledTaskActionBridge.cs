// 文件功能说明：
// 将现有的 IScheduledTask 实现自动桥接为 IInvocableAction。
// 保持向后兼容：已有的代码注册型任务无需修改即可出现在能力目录中。

using Ginkgo.Plugin.Abstractions;

namespace Ginkgo.Infrastructure.Scheduling;

/// <summary>
/// IScheduledTask → IInvocableAction 桥接适配器。
/// 将已有的代码注册型定时任务包装为可调用动作，使其出现在能力目录中。
/// </summary>
public sealed class ScheduledTaskActionBridge : IInvocableAction
{
    private readonly IScheduledTask _task;

    public ScheduledTaskActionBridge(IScheduledTask task)
    {
        _task = task;
    }

    public string ActionKey => _task.TaskKey;
    public string DisplayName => _task.DisplayName;
    public string Category => _task.Group;
    public string? Description => _task.Description;

    // 代码注册型任务不接受外部参数
    public ActionParameterDefinition[] Parameters => Array.Empty<ActionParameterDefinition>();

    public async Task<ActionExecutionResult> ExecuteAsync(ActionContext context)
    {
        // 将 ActionContext 转换为 ScheduledTaskContext
        var taskContext = new ScheduledTaskContext(context.Services, context.CancellationToken, context.Logger);
        await _task.ExecuteAsync(taskContext);
        return ActionExecutionResult.Ok($"内置任务 [{_task.DisplayName}] 执行完成");
    }
}
