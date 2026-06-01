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

    // ===== 默认菜单组维护（每端唯一） =====

    /// <summary>
    /// 将指定菜单组设为默认：校验其 <c>ClientType</c> 为单一终端类型，含逗号分隔多端时抛
    /// <see cref="InvalidOperationException"/>；将同一 <c>ClientType</c> 下其他组 <c>IsDefault</c>
    /// 重置为 0，目标组置 1。
    /// </summary>
    Task SetGroupDefaultAsync(long groupId, CancellationToken ct = default);

    /// <summary>
    /// 查指定终端类型的 <c>IsDefault=1</c> 菜单组 Id（无默认组时返回 null）。
    /// </summary>
    Task<long?> GetDefaultGroupIdAsync(string clientType, CancellationToken ct = default);

    // ===== 统一客户端入口（Portal） =====

    /// <summary>
    /// 构建该端默认菜单组下当前用户可见的入口树（超管返回全部项；非超管按 <c>RequireGrant</c>
    /// 与 item 级授权过滤）。
    /// </summary>
    Task<ClientPortalDto> GetClientPortalAsync(string clientType, long? userId, CancellationToken ct = default);

    // ===== 角色菜单组项（item 级）授权 =====

    /// <summary>
    /// 返回各端 <c>IsDefault=1</c> 默认菜单组下的可授权入口项（供角色编辑器按端分组勾选）。
    /// </summary>
    Task<List<GrantableMenuItemDto>> GetGrantableItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// 查角色已授权的菜单组项 Id 集合。
    /// </summary>
    Task<List<long>> GetRoleMenuGroupItemIdsAsync(long roleId, CancellationToken ct = default);

    /// <summary>
    /// 以提交的菜单组项集合全量覆盖该角色的 item 级授权（去重后写入，保证唯一）。
    /// </summary>
    Task SetRoleMenuGroupItemsAsync(SetRoleMenuGroupItemsInput input, CancellationToken ct = default);

    // ===== 安装链路客户端入口注入 / 清理 =====

    /// <summary>
    /// 供安装链路注入入口项：定位该端 <c>IsDefault=1</c> 菜单组，按
    /// <c>(MenuGroupId, Module, Url)</c> 标识对 <see cref="ClientMenuItemSpec"/> 执行 upsert
    /// （更新已存在项、新增缺失项），无默认组时不创建任何项。
    /// </summary>
    Task UpsertClientMenuItemsAsync(string clientType, string moduleId, IEnumerable<ClientMenuItemSpec> items, CancellationToken ct = default);

    /// <summary>
    /// 按模块归属（<c>Module</c>）过滤查询菜单组项，恰好返回 <c>Module</c> 等于给定值的项集合。
    /// 用于按插件作用域查询入口项（区分大小写匹配）。
    /// </summary>
    Task<List<MenuGroupItemDto>> GetItemsByModuleAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 供卸载链路按 <c>Module</c> 清理入口项：删除 <c>Module=moduleId</c> 的全部
    /// <c>MenuGroupItem</c> 及其级联的 <c>RoleMenuGroupItem</c> 授权关联，不触碰
    /// <c>Module='sys'</c> 项，不删除任何 <c>MenuGroup</c> 记录。
    /// </summary>
    Task RemoveClientMenuItemsByModuleAsync(string moduleId, CancellationToken ct = default);
}
