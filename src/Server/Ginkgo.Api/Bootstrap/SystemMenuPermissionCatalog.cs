using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 系统内置 API 权限目录。
/// 用于补齐初始化 SQL 之外的核心权限资源，确保老库升级后也能走统一权限链路。
/// </summary>
public static class SystemMenuPermissionCatalog
{
    /// <summary>
    /// 模块管理菜单 Id。
    /// </summary>
    public const long ModuleManagementMenuId = 600000000001011;

    /// <summary>
    /// 获取模块管理页下的内置 API 权限资源。
    /// </summary>
    public static IReadOnlyList<SystemMenuPermissionSeed> GetModuleManagementApiPermissions()
    {
        return
        [
            new(
                Id: 600000000010008,
                ParentId: ModuleManagementMenuId,
                Name: "模块配置-保存并热重载",
                Route: "/system/modules:config:save",
                Resource: "/api/v1/modules/config/save-and-reload",
                Method: "POST",
                Order: 208),
            new(
                Id: 600000000010009,
                ParentId: ModuleManagementMenuId,
                Name: "模块配置-重置",
                Route: "/system/modules:config:reset",
                Resource: "/api/v1/modules/config/reset",
                Method: "POST",
                Order: 209),
            new(
                Id: 600000000010010,
                ParentId: ModuleManagementMenuId,
                Name: "模块配置-删除",
                Route: "/system/modules:config:delete",
                Resource: "/api/v1/modules/config/delete",
                Method: "DELETE",
                Order: 210)
        ];
    }

    /// <summary>
    /// 确保模块配置写接口的权限资源已存在。
    /// 老库升级时若缺少这些资源，会导致接口无法按模块管理权限正常授权。
    /// </summary>
    public static async Task EnsureModuleManagementApiPermissionsAsync(
        IRepository<Menu> menuRepo,
        CancellationToken cancellationToken = default)
    {
        var existingMenus = (await menuRepo.GetAllAsync(cancellationToken)).ToList();

        foreach (var definition in GetModuleManagementApiPermissions())
        {
            var exists = existingMenus.Any(m =>
                string.Equals(m.Resource, definition.Resource, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Method, definition.Method, StringComparison.OrdinalIgnoreCase));

            if (exists)
                continue;

            var targetId = existingMenus.Any(m => m.Id == definition.Id)
                ? SnowflakeIdGenerator.NextId()
                : definition.Id;

            var menu = new Menu
            {
                Id = targetId,
                ParentId = definition.ParentId,
                Name = definition.Name,
                Route = definition.Route,
                Type = "Api",
                SupportedClients = definition.SupportedClients,
                Resource = definition.Resource,
                Method = definition.Method,
                Enabled = true,
                Order = definition.Order
            };

            await menuRepo.AddAsync(menu, cancellationToken);
            existingMenus.Add(menu);
        }
    }
}

/// <summary>
/// 系统权限资源种子定义。
/// </summary>
public sealed record SystemMenuPermissionSeed(
    long Id,
    long ParentId,
    string Name,
    string Route,
    string Resource,
    string Method,
    int Order,
    string SupportedClients = "WPF,WEB");
