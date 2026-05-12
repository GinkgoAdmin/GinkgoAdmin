namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 模块权限声明提供者。模块实现此接口以声明所需的权限资源。
/// 安装时自动注册到菜单/权限表。
/// </summary>
public interface IModulePermissionProvider
{
    /// <summary>
    /// 返回模块声明的权限列表。
    /// </summary>
    IReadOnlyList<ModulePermission> GetPermissions();
}

/// <summary>
/// 模块权限声明项。
/// </summary>
public sealed class ModulePermission
{
    /// <summary>
    /// API 资源路径（如 /api/v1/orders）。
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法（GET/POST/PUT/DELETE）。
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// 权限显示名称（如 "订单查询"）。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 父级菜单路径（用于归组，如 "订单管理"）。
    /// </summary>
    public string? ParentPath { get; set; }
}
