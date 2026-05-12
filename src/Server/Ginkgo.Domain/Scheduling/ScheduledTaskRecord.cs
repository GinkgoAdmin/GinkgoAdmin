// 文件功能说明：
// 定义定时任务注册记录实体，映射到 ginkgo_Sys_ScheduledTask 表。

using SqlSugar;

namespace Ginkgo.Domain.Scheduling;

/// <summary>
/// 定时任务注册记录（持久化到数据库，保存运行时状态与用户自定义配置）。
/// </summary>
[SugarTable("ginkgo_Sys_ScheduledTask", TableDescription = "定时任务注册表")]
[SugarIndex("UX_ScheduledTask_TaskKey", nameof(TaskKey), OrderByType.Asc, true)]
public sealed class ScheduledTaskRecord : Entity
{
    /// <summary>
    /// 任务唯一标识（如 System.VerificationCodeCleanup）。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false, ColumnDescription = "任务唯一标识")]
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>
    /// 任务显示名称。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false, ColumnDescription = "任务显示名称")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 任务分组。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "任务分组")]
    public string? Group { get; set; }

    /// <summary>
    /// Cron 表达式（5 段式，可被管理员在后台修改覆盖代码默认值）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "Cron 表达式")]
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 上次执行时间。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "上次执行时间")]
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// 下次计划执行时间。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "下次计划执行时间")]
    public DateTime? NextRunAt { get; set; }

    /// <summary>
    /// 上次执行结果（Success / Failed / Running）。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "上次执行结果")]
    public string? LastResult { get; set; }

    /// <summary>
    /// 上次执行耗时（毫秒）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "上次执行耗时(ms)")]
    public int? LastElapsedMs { get; set; }

    /// <summary>
    /// 任务描述。
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "任务描述")]
    public string? Description { get; set; }

    /// <summary>
    /// 执行类型（如"内置方法"、"HTTP 回调"、"SQL 脚本"等）。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "执行类型")]
    public string? ExecutionType { get; set; }

    /// <summary>
    /// 执行目标描述（类全名、URL、SQL 等）。
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "执行目标描述")]
    public string? ExecutionTarget { get; set; }

    /// <summary>
    /// 任务定义类型：CodeBased（代码注册）或 Dynamic（动态配置）。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "任务定义类型（CodeBased/Dynamic）")]
    public string? DefinitionType { get; set; }

    /// <summary>
    /// 执行源标识（对应 ITaskExecutionProvider.SourceKey，如 Action/Http/Sql/AIPrompt）。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "执行源标识")]
    public string? ExecutionSource { get; set; }

    /// <summary>
    /// 引用的动作键（ExecutionSource=Action 时使用）。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "引用的动作键")]
    public string? ActionKey { get; set; }

    /// <summary>
    /// 动态任务的完整执行配置（JSON 格式，存储提供器所需的所有参数）。
    /// </summary>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "执行配置JSON")]
    public string? ConfigJson { get; set; }

    /// <summary>
    /// 任务来源（BuiltIn / 模块Id / 插件名称）。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "任务来源")]
    public string? Source { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "更新时间")]
    public DateTime? UpdatedAt { get; set; }
}
