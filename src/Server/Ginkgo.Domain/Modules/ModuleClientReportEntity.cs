using SqlSugar;

namespace Ginkgo.Domain.Modules;

[SugarTable("ginkgo_Modules_ClientReport", TableDescription = "模块客户端上报记录表")]
public sealed class ModuleClientReportEntity : Entity
{
    [SugarColumn(ColumnDescription = "模块ID（与模块清单Id一致）", Length = 128, IsNullable = false)]
    public string ModuleId { get; set; } = string.Empty;

    [SugarColumn(ColumnDescription = "客户端唯一标识", Length = 128, IsNullable = false)]
    public string ClientId { get; set; } = string.Empty;

    [SugarColumn(ColumnDescription = "客户端上报的模块版本", Length = 64, IsNullable = true)]
    public string? Version { get; set; }

    [SugarColumn(ColumnDescription = "客户端状态（如 Loaded/Installed/Error 等）", Length = 64, IsNullable = false)]
    public string Status { get; set; } = "Unknown";

    [SugarColumn(ColumnDescription = "错误信息（可空）", Length = 1024, IsNullable = true)]
    public string? Error { get; set; }

    [SugarColumn(ColumnDescription = "上报时间（UTC）", IsNullable = false)]
    public DateTime ReportedAtUtc { get; set; }
}

