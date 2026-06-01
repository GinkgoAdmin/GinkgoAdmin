// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试用的辅助数据模型。
// 这些模型用于生成器在内存仓储中构造场景数据，并描述后续属性测试所需的输入（如客户端入口声明项）。
// 注意：ClientMenuItemSpec 的字段与设计文档《Components and Interfaces 3.3》一致（Title/Icon/Path/RequireGrant/Order/Badge），
//       在安装链路对应实现尚未落地时，先在测试侧定义同形输入，供注入相关属性测试复用。

using System.Collections.Generic;

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 终端类型常量（菜单组存储侧的归一化取值）。
/// </summary>
public static class PortalClientTypes
{
    /// <summary>移动端（接口入参 MOBILE 归一化后的存储值）。</summary>
    public const string Uniapp = "UNIAPP";

    /// <summary>WEB 前台。</summary>
    public const string WebPortal = "WEB_PORTAL";

    /// <summary>桌面端。</summary>
    public const string Wpf = "WPF";

    /// <summary>三个单一终端类型集合，供生成器随机选取。</summary>
    public static readonly string[] Single = { Uniapp, WebPortal, Wpf };
}

/// <summary>
/// 客户端入口声明项（与 install.json 的 ClientMenus.items 同形）。
/// 供「注入字段映射 / 幂等 / install-uninstall 往返」等属性测试作为输入。
/// </summary>
public sealed record ClientMenuItemSpec(
    string Title,
    string? Icon,
    string Path,
    bool RequireGrant,
    int Order,
    string? Badge);

/// <summary>
/// 一条客户端入口声明（对应某个终端类型下的一组入口项）。
/// </summary>
public sealed record ClientMenusSpec(
    string ClientType,
    List<ClientMenuItemSpec> Items);

/// <summary>
/// 用户角色配置场景：用于 portal 可见性属性测试。
/// </summary>
/// <param name="UserId">用户 Id。</param>
/// <param name="RoleIds">用户拥有的角色 Id 集合（可空集合表示无角色）。</param>
/// <param name="IsSuperAdmin">用户所属角色中是否含超管角色。</param>
public sealed record UserRoleScenario(
    long UserId,
    List<long> RoleIds,
    bool IsSuperAdmin);
