// 文件功能说明：
// 定义定时任务执行日志实体，映射到 ginkgo_Sys_ScheduledTaskLog 表。

using SqlSugar;

namespace Ginkgo.Domain.Scheduling;

/// <summary>
/// 定时任务执行日志记录。
/// </summary>
[SugarTable("ginkgo_Sys_ScheduledTaskLog", TableDescription = "定时任务执行日志表")]
[SugarIndex("IX_TaskLog_TaskKey_StartedAt", nameof(TaskKey), OrderByType.Asc, nameof(StartedAt), OrderByType.Desc)]
public sealed class ScheduledTaskLog : Entity
{
    /// <summary>
    /// 任务唯一标识。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false, ColumnDescription = "任务唯一标识")]
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>
    /// 执行开始时间。
    /// </summary>
    [SugarColumn(ColumnDescription = "执行开始时间")]
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 执行结束时间。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "执行结束时间")]
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// 是否执行成功。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否执行成功")]
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息（失败时记录）。
    /// </summary>
    [SugarColumn(Length = 2000, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "执行耗时(ms)")]
    public int? ElapsedMs { get; set; }

    /// <summary>
    /// 触发方式（Auto / Manual）。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "触发方式（Auto/Manual）")]
    public string? TriggerType { get; set; }

    /// <summary>
    /// 执行详情 JSON（HTTP 响应、SQL 影响行数、能力返回数据等）。
    /// </summary>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "执行详情JSON")]
    public string? DetailsJson { get; set; }
}
