// 文件功能说明：
// 定时任务应用层 DTO 定义。

namespace Ginkgo.Application.Scheduling;

/// <summary>
/// 定时任务列表项。
/// </summary>
public sealed class ScheduledTaskItemDto
{
    public long Id { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastResult { get; set; }
    public int? LastElapsedMs { get; set; }
    public string? Description { get; set; }
    public string? ExecutionType { get; set; }
    public string? ExecutionTarget { get; set; }
    public string? DefinitionType { get; set; }
    public string? ExecutionSource { get; set; }
    public string? ActionKey { get; set; }
    public string? ConfigJson { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 更新任务配置输入。
/// </summary>
public sealed class UpdateScheduledTaskInput
{
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Cron 表达式。
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述。
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 任务执行日志列表项。
/// </summary>
public sealed class ScheduledTaskLogItemDto
{
    public long Id { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ElapsedMs { get; set; }
    public string? TriggerType { get; set; }
    public string? DetailsJson { get; set; }
}

/// <summary>
/// 创建动态任务输入。
/// </summary>
public sealed class CreateDynamicTaskInput
{
    /// <summary>
    /// 任务显示名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 分组。
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Cron 表达式。
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 描述。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 执行源标识（对应 ITaskExecutionProvider.SourceKey，如 Action/Http/Sql）。
    /// </summary>
    public string ExecutionSource { get; set; } = string.Empty;

    /// <summary>
    /// 执行配置 JSON。
    /// </summary>
    public string ConfigJson { get; set; } = "{}";
}

/// <summary>
/// 执行提供器信息（返回给前端用于渲染新增弹窗）。
/// </summary>
public sealed class ExecutionProviderDto
{
    public string SourceKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public bool SupportsTest { get; set; }
    public object? FormDefinition { get; set; }
}

/// <summary>
/// 可调用动作信息（返回给前端用于能力选择器）。
/// </summary>
public sealed class InvocableActionDto
{
    public string ActionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public object[]? Parameters { get; set; }
}

/// <summary>
/// 执行结果 DTO（测试执行返回）。
/// </summary>
public sealed class ActionExecutionResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
}
