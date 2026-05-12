// 文件功能说明：
// 定义菜单组应用服务接口。

using Ginkgo.Shared;

namespace Ginkgo.Application.Menus;

/// <summary>
/// 菜单组应用服务接口。
/// </summary>
public interface IMenuGroupAppService
{
    // ===== 菜单组管理 =====

    /// <summary>
    /// 获取菜单组列表（含每组的菜单项数量）。
    /// </summary>
    Task<List<MenuGroupListItemDto>> GetGroupListAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取菜单组详情。
    /// </summary>
    Task<MenuGroupDetailDto?> GetGroupAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 创建菜单组。
    /// </summary>
    Task<long> CreateGroupAsync(CreateMenuGroupInput input, CancellationToken ct = default);

    /// <summary>
    /// 更新菜单组。
    /// </summary>
    Task UpdateGroupAsync(long id, UpdateMenuGroupInput input, CancellationToken ct = default);

    /// <summary>
    /// 删除菜单组（系统内置不可删除）。
    /// </summary>
    Task DeleteGroupAsync(long id, CancellationToken ct = default);

    // ===== 菜单组项管理 =====

    /// <summary>
    /// 获取指定菜单组下的菜单项树。
    /// </summary>
    Task<List<MenuGroupItemDto>> GetItemTreeAsync(long groupId, CancellationToken ct = default);

    /// <summary>
    /// 获取单个菜单组项详情。
    /// </summary>
    Task<MenuGroupItemDto?> GetItemAsync(long groupId, long id, CancellationToken ct = default);

    /// <summary>
    /// 创建菜单组项。
    /// </summary>
    Task<long> CreateItemAsync(long groupId, CreateMenuGroupItemInput input, CancellationToken ct = default);

    /// <summary>
    /// 更新菜单组项。
    /// </summary>
    Task UpdateItemAsync(long groupId, long id, UpdateMenuGroupItemInput input, CancellationToken ct = default);

    /// <summary>
    /// 删除菜单组项。
    /// </summary>
    Task DeleteItemAsync(long groupId, long id, CancellationToken ct = default);

    /// <summary>
    /// 批量删除菜单组项。
    /// </summary>
    Task BatchDeleteItemsAsync(long groupId, long[] ids, CancellationToken ct = default);

    /// <summary>
    /// 批量更新排序（拖拽排序）。
    /// </summary>
    Task SortItemsAsync(long groupId, List<MenuGroupItemSortInput> items, CancellationToken ct = default);

    /// <summary>
    /// 从系统菜单导入到菜单组。
    /// </summary>
    Task<List<long>> ImportFromSystemMenuAsync(long groupId, long[] menuIds, long? parentId, CancellationToken ct = default);

    // ===== 导航查询（公开接口） =====

    /// <summary>
    /// 按 Slug 获取导航菜单（含权限过滤）。
    /// </summary>
    Task<NavigationMenuDto?> GetNavigationAsync(string slug, long? userId, CancellationToken ct = default);

    // ===== 角色菜单组权限 =====

    /// <summary>
    /// 获取角色已授权的菜单组 Id 列表。
    /// </summary>
    Task<List<long>> GetRoleMenuGroupIdsAsync(long roleId, CancellationToken ct = default);

    /// <summary>
    /// 设置角色的菜单组权限。
    /// </summary>
    Task SetRoleMenuGroupsAsync(SetRoleMenuGroupsInput input, CancellationToken ct = default);
}
