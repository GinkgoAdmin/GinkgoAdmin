// 文件功能说明：
// 菜单组应用服务实现，包含菜单组 CRUD、菜单项管理、导航查询（含权限过滤）和角色授权。

using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;

namespace Ginkgo.Application.Menus;

/// <summary>
/// 菜单组应用服务实现。
/// </summary>
public sealed class MenuGroupAppService : IMenuGroupAppService
{
    private readonly IRepository<MenuGroup> _groupRepo;
    private readonly IRepository<MenuGroupItem> _itemRepo;
    private readonly IRepository<RoleMenuGroup> _roleMenuGroupRepo;
    private readonly IRepository<Menu> _menuRepo;
    private readonly IRepository<UserRole> _userRoleRepo;
    private readonly IRepository<Role> _roleRepo;
    private readonly IRepository<RolePermission> _rolePermRepo;

    public MenuGroupAppService(
        IRepository<MenuGroup> groupRepo,
        IRepository<MenuGroupItem> itemRepo,
        IRepository<RoleMenuGroup> roleMenuGroupRepo,
        IRepository<Menu> menuRepo,
        IRepository<UserRole> userRoleRepo,
        IRepository<Role> roleRepo,
        IRepository<RolePermission> rolePermRepo)
    {
        _groupRepo = groupRepo;
        _itemRepo = itemRepo;
        _roleMenuGroupRepo = roleMenuGroupRepo;
        _menuRepo = menuRepo;
        _userRoleRepo = userRoleRepo;
        _roleRepo = roleRepo;
        _rolePermRepo = rolePermRepo;
    }

    // ===== 菜单组管理 =====

    public Task<List<MenuGroupListItemDto>> GetGroupListAsync(CancellationToken ct = default)
    {
        var groups = _groupRepo.Query()
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var groupIds = groups.Select(g => g.Id).ToList();
        // 统计每个组的菜单项数量
        var allItems = _itemRepo.Query()
            .Where(x => groupIds.Contains(x.MenuGroupId))
            .Select(x => x.MenuGroupId)
            .ToList();
        var itemCounts = allItems.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(groups.Select(g => new MenuGroupListItemDto
        {
            Id = g.Id,
            Name = g.Name,
            Slug = g.Slug,
            Description = g.Description,
            Location = g.Location,
            ClientType = g.ClientType,
            IsSystem = g.IsSystem,
            Enabled = g.Enabled,
            MaxDepth = g.MaxDepth,
            Version = g.Version,
            ItemCount = itemCounts.TryGetValue(g.Id, out var c) ? c : 0
        }).ToList());
    }

    public async Task<MenuGroupDetailDto?> GetGroupAsync(long id, CancellationToken ct = default)
    {
        var entity = await _groupRepo.GetByIdAsync(id, ct);
        if (entity == null) return null;
        return new MenuGroupDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            Description = entity.Description,
            Location = entity.Location,
            ClientType = entity.ClientType,
            IsSystem = entity.IsSystem,
            Enabled = entity.Enabled,
            MaxDepth = entity.MaxDepth,
            Version = entity.Version
        };
    }

    public async Task<long> CreateGroupAsync(CreateMenuGroupInput input, CancellationToken ct = default)
    {
        // Slug 唯一性校验
        var existsSlug = _groupRepo.Query().Any(x => x.Slug == input.Slug.Trim().ToLowerInvariant());
        if (existsSlug) throw new InvalidOperationException($"菜单组标识已存在: {input.Slug}");

        var entity = MenuGroup.Create(
            name: input.Name,
            slug: input.Slug,
            description: input.Description,
            location: input.Location,
            clientType: input.ClientType,
            version: input.Version
        );
        entity.MaxDepth = input.MaxDepth;

        await _groupRepo.AddAsync(entity, ct);
        return entity.Id;
    }

    public async Task UpdateGroupAsync(long id, UpdateMenuGroupInput input, CancellationToken ct = default)
    {
        var entity = await _groupRepo.GetByIdAsync(id, ct);
        if (entity == null) return;

        // Slug 唯一性校验（排除自身）
        var slugNorm = input.Slug.Trim().ToLowerInvariant();
        if (!string.Equals(entity.Slug, slugNorm, StringComparison.OrdinalIgnoreCase))
        {
            var existsSlug = _groupRepo.Query().Any(x => x.Id != id && x.Slug == slugNorm);
            if (existsSlug) throw new InvalidOperationException($"菜单组标识已存在: {input.Slug}");
        }

        entity.UpdateMeta(input.Name, input.Slug, input.Description, input.Location,
            input.ClientType, input.Version, input.MaxDepth);
        if (input.Enabled) entity.Enable(); else entity.Disable();

        await _groupRepo.UpdateAsync(entity, ct);
    }

    public async Task DeleteGroupAsync(long id, CancellationToken ct = default)
    {
        var entity = await _groupRepo.GetByIdAsync(id, ct);
        if (entity == null) return;
        if (entity.IsSystem) throw new InvalidOperationException("系统内置菜单组不可删除");

        // 级联删除菜单组项
        var items = _itemRepo.Query().Where(x => x.MenuGroupId == id).ToList();
        foreach (var item in items)
        {
            await _itemRepo.DeleteAsync(item.Id, ct);
        }
        // 级联删除角色菜单组权限
        var roleGroups = _roleMenuGroupRepo.Query().Where(x => x.MenuGroupId == id).ToList();
        foreach (var rg in roleGroups)
        {
            await _roleMenuGroupRepo.DeleteAsync(rg.Id, ct);
        }

        await _groupRepo.DeleteAsync(id, ct);
    }

    // ===== 菜单组项管理 =====

    public Task<List<MenuGroupItemDto>> GetItemTreeAsync(long groupId, CancellationToken ct = default)
    {
        var items = _itemRepo.Query()
            .Where(x => x.MenuGroupId == groupId)
            .OrderBy(x => x.Order)
            .ToList();

        // 批量查关联的系统菜单名称
        var refMenuIds = items.Where(x => x.RefMenuId.HasValue).Select(x => x.RefMenuId!.Value).Distinct().ToList();
        Dictionary<long, string> refMenuNames;
        if (refMenuIds.Count > 0)
        {
            refMenuNames = _menuRepo.Query().Where(x => refMenuIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name }).ToList()
                .ToDictionary(x => x.Id, x => x.Name);
        }
        else
        {
            refMenuNames = new Dictionary<long, string>();
        }

        var dtos = items.Select(x => new MenuGroupItemDto
        {
            Id = x.Id,
            MenuGroupId = x.MenuGroupId,
            ParentId = x.ParentId,
            Title = x.Title,
            TitleI18n = x.TitleI18n,
            Subtitle = x.Subtitle,
            Icon = x.Icon,
            Image = x.Image,
            LinkType = x.LinkType,
            Url = x.Url,
            Target = x.Target,
            RefMenuId = x.RefMenuId,
            RefMenuName = x.RefMenuId.HasValue && refMenuNames.TryGetValue(x.RefMenuId.Value, out var n) ? n : null,
            PermissionCode = x.PermissionCode,
            CssClass = x.CssClass,
            Badge = x.Badge,
            BadgeType = x.BadgeType,
            ExtraData = x.ExtraData,
            Order = x.Order,
            Enabled = x.Enabled
        }).ToList();

        return Task.FromResult(BuildTree(dtos));
    }

    public async Task<MenuGroupItemDto?> GetItemAsync(long groupId, long id, CancellationToken ct = default)
    {
        var entity = await _itemRepo.GetByIdAsync(id, ct);
        if (entity == null || entity.MenuGroupId != groupId) return null;

        string? refMenuName = null;
        if (entity.RefMenuId.HasValue)
        {
            var refMenu = await _menuRepo.GetByIdAsync(entity.RefMenuId.Value, ct);
            refMenuName = refMenu?.Name;
        }

        return new MenuGroupItemDto
        {
            Id = entity.Id,
            MenuGroupId = entity.MenuGroupId,
            ParentId = entity.ParentId,
            Title = entity.Title,
            TitleI18n = entity.TitleI18n,
            Subtitle = entity.Subtitle,
            Icon = entity.Icon,
            Image = entity.Image,
            LinkType = entity.LinkType,
            Url = entity.Url,
            Target = entity.Target,
            RefMenuId = entity.RefMenuId,
            RefMenuName = refMenuName,
            PermissionCode = entity.PermissionCode,
            CssClass = entity.CssClass,
            Badge = entity.Badge,
            BadgeType = entity.BadgeType,
            ExtraData = entity.ExtraData,
            Order = entity.Order,
            Enabled = entity.Enabled
        };
    }

    public async Task<long> CreateItemAsync(long groupId, CreateMenuGroupItemInput input, CancellationToken ct = default)
    {
        // 校验菜单组存在
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null) throw new InvalidOperationException("菜单组不存在");

        var entity = MenuGroupItem.Create(
            menuGroupId: groupId,
            title: input.Title,
            linkType: input.LinkType,
            url: input.Url,
            parentId: input.ParentId,
            refMenuId: input.RefMenuId
        );
        entity.TitleI18n = input.TitleI18n;
        entity.Subtitle = input.Subtitle?.Trim();
        entity.Icon = input.Icon?.Trim();
        entity.Image = input.Image?.Trim();
        entity.Target = string.IsNullOrWhiteSpace(input.Target) ? "_self" : input.Target.Trim();
        entity.PermissionCode = input.PermissionCode?.Trim();
        entity.CssClass = input.CssClass?.Trim();
        entity.Badge = input.Badge?.Trim();
        entity.BadgeType = input.BadgeType?.Trim();
        entity.ExtraData = string.IsNullOrWhiteSpace(input.ExtraData) ? null : input.ExtraData;
        entity.SetOrder(input.Order);

        await _itemRepo.AddAsync(entity, ct);
        return entity.Id;
    }

    public async Task UpdateItemAsync(long groupId, long id, UpdateMenuGroupItemInput input, CancellationToken ct = default)
    {
        var entity = await _itemRepo.GetByIdAsync(id, ct);
        if (entity == null || entity.MenuGroupId != groupId) return;

        entity.UpdateMeta(
            title: input.Title,
            subtitle: input.Subtitle,
            icon: input.Icon,
            image: input.Image,
            linkType: input.LinkType,
            url: input.Url,
            target: input.Target,
            refMenuId: input.RefMenuId,
            permissionCode: input.PermissionCode,
            cssClass: input.CssClass,
            badge: input.Badge,
            badgeType: input.BadgeType,
            extraData: string.IsNullOrWhiteSpace(input.ExtraData) ? null : input.ExtraData
        );
        entity.MoveTo(input.ParentId);
        entity.SetOrder(input.Order);
        if (input.Enabled) entity.Enable(); else entity.Disable();

        await _itemRepo.UpdateAsync(entity, ct);
    }

    public async Task DeleteItemAsync(long groupId, long id, CancellationToken ct = default)
    {
        var entity = await _itemRepo.GetByIdAsync(id, ct);
        if (entity == null || entity.MenuGroupId != groupId) return;

        // 递归删除子项
        var children = _itemRepo.Query().Where(x => x.ParentId == id).ToList();
        foreach (var child in children)
        {
            await DeleteItemAsync(groupId, child.Id, ct);
        }

        await _itemRepo.DeleteAsync(id, ct);
    }

    public async Task BatchDeleteItemsAsync(long groupId, long[] ids, CancellationToken ct = default)
    {
        foreach (var id in ids.Distinct())
        {
            await DeleteItemAsync(groupId, id, ct);
        }
    }

    public async Task SortItemsAsync(long groupId, List<MenuGroupItemSortInput> items, CancellationToken ct = default)
    {
        foreach (var sortItem in items)
        {
            var entity = await _itemRepo.GetByIdAsync(sortItem.Id, ct);
            if (entity == null || entity.MenuGroupId != groupId) continue;
            entity.MoveTo(sortItem.ParentId);
            entity.SetOrder(sortItem.Order);
            await _itemRepo.UpdateAsync(entity, ct);
        }
    }

    public async Task<List<long>> ImportFromSystemMenuAsync(long groupId, long[] menuIds, long? parentId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null) throw new InvalidOperationException("菜单组不存在");

        var result = new List<long>();
        var order = _itemRepo.Query().Where(x => x.MenuGroupId == groupId && x.ParentId == parentId).Count();

        foreach (var menuId in menuIds)
        {
            var menu = await _menuRepo.GetByIdAsync(menuId, ct);
            if (menu == null) continue;

            var item = MenuGroupItem.Create(
                menuGroupId: groupId,
                title: menu.Name,
                linkType: "SystemMenu",
                url: menu.Route ?? menu.WebRouteUrl,
                parentId: parentId,
                refMenuId: menu.Id
            );
            item.Icon = menu.Icon;
            item.SetOrder(order++);

            await _itemRepo.AddAsync(item, ct);
            result.Add(item.Id);
        }

        return result;
    }

    // ===== 导航查询（含权限过滤） =====

#pragma warning disable CS1998 // 此方法中所有查询均为同步操作，暂不需要 await
    public async Task<NavigationMenuDto?> GetNavigationAsync(string slug, long? userId, CancellationToken ct = default)
    {
        var group = _groupRepo.Query().FirstOrDefault(x => x.Slug == slug && x.Enabled);
        if (group == null) return null;

        // 权限检查
        bool isSuperAdmin = false;
        var userRoleIds = new List<long>();
        var userPermissionIds = new HashSet<long>();
        var userPermissionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (userId.HasValue)
        {
            userRoleIds = _userRoleRepo.Query().Where(x => x.UserId == userId.Value).Select(x => x.RoleId).ToList();
            if (userRoleIds.Count > 0)
            {
                isSuperAdmin = _roleRepo.Query().Any(x => userRoleIds.Contains(x.Id) && x.IsSuperAdmin);
            }

            if (!isSuperAdmin)
            {
                // 检查角色是否有权访问该菜单组
                var hasGroupAccess = _roleMenuGroupRepo.Query()
                    .Any(x => userRoleIds.Contains(x.RoleId) && x.MenuGroupId == group.Id);
                if (!hasGroupAccess) return null;

                // 获取用户拥有的权限 Id（用于 SystemMenu 类型过滤）
                userPermissionIds = _rolePermRepo.Query()
                    .Where(x => userRoleIds.Contains(x.RoleId))
                    .Select(x => x.PermissionId)
                    .ToList()
                    .ToHashSet();

                // 获取用户拥有的 Code（通过系统菜单的 Code 字段，部分菜单通过 Code 标识权限）
                if (userPermissionIds.Count > 0)
                {
                    userPermissionCodes = _menuRepo.Query()
                        .Where(x => userPermissionIds.Contains(x.Id) && x.Code != null)
                        .Select(x => x.Code!)
                        .ToList()
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        // 查询菜单项
        var items = _itemRepo.Query()
            .Where(x => x.MenuGroupId == group.Id && x.Enabled)
            .OrderBy(x => x.Order)
            .ToList();

        // 权限过滤（非超管）
        if (!isSuperAdmin && userId.HasValue)
        {
            items = items.Where(item =>
            {
                // SystemMenu 类型：检查 RolePermission 中是否有 RefMenuId
                if (item.LinkType == "SystemMenu" && item.RefMenuId.HasValue)
                {
                    return userPermissionIds.Contains(item.RefMenuId.Value);
                }
                // 有 PermissionCode：检查角色是否拥有该编码
                if (!string.IsNullOrWhiteSpace(item.PermissionCode))
                {
                    return userPermissionCodes.Contains(item.PermissionCode);
                }
                // 无权限控制：直接保留
                return true;
            }).ToList();
        }

        // 组装树
        var navItems = items.Select(x => new NavigationItemDto
        {
            Id = x.Id,
            Title = x.Title,
            TitleI18n = x.TitleI18n,
            Subtitle = x.Subtitle,
            Icon = x.Icon,
            Image = x.Image,
            Url = x.Url,
            Target = x.Target,
            CssClass = x.CssClass,
            Badge = x.Badge,
            BadgeType = x.BadgeType,
            ExtraData = x.ExtraData
        }).ToList();

        // 构建简易映射用于树组装
        var navItemMap = new Dictionary<long, NavigationItemDto>();
        for (int i = 0; i < items.Count; i++)
        {
            navItemMap[items[i].Id] = navItems[i];
        }

        var roots = new List<NavigationItemDto>();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var navItem = navItems[i];
            if (item.ParentId.HasValue && navItemMap.TryGetValue(item.ParentId.Value, out var parent))
            {
                parent.Children ??= new List<NavigationItemDto>();
                parent.Children.Add(navItem);
            }
            else
            {
                roots.Add(navItem);
            }
        }

        return new NavigationMenuDto
        {
            Slug = group.Slug,
            Name = group.Name,
            Location = group.Location,
            Version = group.Version,
            Items = roots
        };
    }
#pragma warning restore CS1998

    // ===== 角色菜单组权限 =====

    public Task<List<long>> GetRoleMenuGroupIdsAsync(long roleId, CancellationToken ct = default)
    {
        return Task.FromResult(_roleMenuGroupRepo.Query()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.MenuGroupId)
            .ToList());
    }

    public async Task SetRoleMenuGroupsAsync(SetRoleMenuGroupsInput input, CancellationToken ct = default)
    {
        // 删除旧的授权关系
        var existing = _roleMenuGroupRepo.Query().Where(x => x.RoleId == input.RoleId).ToList();
        foreach (var e in existing)
        {
            await _roleMenuGroupRepo.DeleteAsync(e.Id, ct);
        }

        // 新增授权关系
        foreach (var groupId in input.MenuGroupIds.Distinct())
        {
            var entity = new RoleMenuGroup
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                RoleId = input.RoleId,
                MenuGroupId = groupId,
                CreatedAt = DateTime.Now
            };
            await _roleMenuGroupRepo.AddAsync(entity, ct);
        }
    }

    // ===== 辅助方法 =====

    /// <summary>
    /// 平铺列表构建树形结构。
    /// </summary>
    private static List<MenuGroupItemDto> BuildTree(List<MenuGroupItemDto> items)
    {
        var map = items.ToDictionary(x => x.Id);
        var roots = new List<MenuGroupItemDto>();

        foreach (var item in items)
        {
            if (item.ParentId.HasValue && map.TryGetValue(item.ParentId.Value, out var parent))
            {
                parent.Children ??= new List<MenuGroupItemDto>();
                parent.Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return roots;
    }
}
