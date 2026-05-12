using SqlSugar;
using Ginkgo.Domain;


namespace Ginkgo.Domain.Modules;

[SugarTable("ginkgo_Modules_StatusLog", TableDescription = "模块状态日志表：记录服务端/客户端/菜单/加载等状态快照")]
public sealed class ModuleStatusLogEntity : Entity
{

    [SugarColumn(Length = 100, IsNullable = false, ColumnDescription = "模块标识（与安装表 ModuleId 对应）")]
    public string ModuleId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false, ColumnDescription = "检查时间（UTC）")]
    public DateTime CheckedAtUtc { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "服务端 DLL 是否存在/可用")]
    public bool ServerDllLoaded { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "服务端配置是否完整正确（config/*.json 存在）")]
    public bool ServerConfigOk { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "当前程序运行时是否已加载（受 Enabled 控制）")]
    public bool LoadedInRuntime { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "客户端模块是否存在（client 目录存在且版本匹配）")]
    public bool ClientPresent { get; set; }

    [SugarColumn(IsNullable = false, ColumnDescription = "菜单是否已注册（install.json 执行情况，后续可联查菜单表）")]
    public bool MenuRegistered { get; set; }

    [SugarColumn(Length = 1000, IsNullable = true, ColumnDescription = "错误信息（如有）")]
    public string? Error { get; set; }

    [SugarColumn(IsNullable = true, ColumnDescription = "详细检查结果 JSON（预留）")]
    public string? DetailsJson { get; set; }
}

