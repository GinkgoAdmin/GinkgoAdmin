using SqlSugar;

namespace Ginkgo.Domain.Modules;

[SugarTable("ginkgo_Modules_OpLog", TableDescription = "模块操作日志表：记录加载/热重载/卸载/注入等生命周期事件")]
public sealed class ModuleOpLogEntity : Entity
{
    [SugarColumn(Length = 128, IsNullable = false, ColumnDescription = "模块ID（与模块清单Id一致）")]
    public string ModuleId { get; set; } = string.Empty;

    [SugarColumn(Length = 40, IsNullable = false, ColumnDescription = "操作类型（Load/Reload/Unload/Inject.Success/Inject.Fail 等）")]
    public string Action { get; set; } = string.Empty;

    [SugarColumn(Length = 10, IsNullable = false, ColumnDescription = "日志级别（INFO/ERROR）")]
    public string Level { get; set; } = "INFO";

    [SugarColumn(IsNullable = false, ColumnDescription = "发生时间（UTC）")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.Now;

    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "简要信息")]
    public string? Message { get; set; }

    [SugarColumn(IsNullable = true, ColumnDescription = "详细信息/上下文（可为JSON）")]
    public string? DetailsJson { get; set; }
}

