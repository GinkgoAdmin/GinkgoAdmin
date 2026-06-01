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
    private readonly IRepository<RoleMenuGroupItem> _roleMenuGroupItemRepo;

    public MenuGroupAppService(
        IRepository<MenuGroup> groupRepo,
        IRepository<MenuGroupItem> itemRepo,
        IRepository<RoleMenuGroup> roleMenuGroupRepo,
        IRepository<Menu> menuRepo,
        IRepository<UserRole> userRoleRepo,
        IRepository<Role> roleRepo,
        IRepository<RolePermission> rolePermRepo,
        IRepository<RoleMenuGroupItem> roleMenuGroupItemRepo)
    {
        _groupRepo = groupRepo;
        _itemRepo = itemRepo;
        _roleMenuGroupRepo = roleMenuGroupRepo;
        _menuRepo = menuRepo;
        _userRoleRepo = userRoleRepo;
        _roleRepo = roleRepo;
        _rolePermRepo = rolePermRepo;
        _roleMenuGroupItemRepo = roleMenuGroupItemRepo;
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
        if (items.Count > 0)
        {
            var itemIds = items.Select(x => x.Id).ToList();

            // 先级联删除这些菜单组项的 item 级角色授权关联（RoleMenuGroupItem），避免残留孤儿授权（需求 8.7）
            var roleItemIds = _roleMenuGroupItemRepo.Query()
                .Where(x => itemIds.Contains(x.MenuGroupItemId))
                .Select(x => x.Id)
                .ToList();
            if (roleItemIds.Count > 0)
            {
                await _roleMenuGroupItemRepo.DeleteRangeAsync(roleItemIds, ct);
            }

            // 再删除菜单组项
            foreach (var item in items)
            {
                await _itemRepo.DeleteAsync(item.Id, ct);
            }
        }
        // 级联删除角色菜单组权限（组级 RoleMenuGroup）
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
            Enabled = x.Enabled,
            Module = x.Module,
            RequireGrant = x.RequireGrant
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
            Enabled = entity.Enabled,
            Module = entity.Module,
            RequireGrant = entity.RequireGrant
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

        // 级联清理该菜单组项的 item 级角色授权关联（RoleMenuGroupItem），避免残留孤儿授权（需求 8.7）
        var roleItemIds = _roleMenuGroupItemRepo.Query()
            .Where(x => x.MenuGroupItemId == id)
            .Select(x => x.Id)
            .ToList();
        if (roleItemIds.Count > 0)
        {
            await _roleMenuGroupItemRepo.DeleteRangeAsync(roleItemIds, ct);
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

    // ===== 默认菜单组维护（每端唯一） =====
    // 说明：以下方法的真实实现由后续任务（6.1/6.2 入口注入与清理、
    // 7.1 项级授权、9.1 portal 入口树）分波次落地，此处先提供占位实现以保证编译通过。

    /// <summary>
    /// 将指定菜单组设为该终端类型的默认菜单组。
    /// 校验目标组 <c>ClientType</c> 为单一终端类型（含逗号分隔多端时拒绝）；
    /// 在同一逻辑工作单元内，先将同端其他组 <c>IsDefault</c> 重置为 0，再将目标组置 1，
    /// 保证每个终端类型下至多一个 <c>IsDefault=1</c> 菜单组。
    /// </summary>
    public async Task SetGroupDefaultAsync(long groupId, CancellationToken ct = default)
    {
        var target = await _groupRepo.GetByIdAsync(groupId, ct);
        if (target == null) throw new InvalidOperationException("菜单组不存在");

        // 校验目标组 ClientType 为单一终端类型；空或含逗号分隔多端时一律拒绝
        var singleClientType = ExtractSingleClientType(target.ClientType);
        if (singleClientType == null)
        {
            throw new InvalidOperationException("请为每个终端类型单独设置默认菜单组");
        }

        // 同一逻辑工作单元内先重置同端其他组、再置目标组为默认，避免出现瞬时多默认组
        // 同端判定：按归一化后的单一终端类型匹配（排除目标组自身）
        var sameClientDefaults = _groupRepo.Query()
            .Where(x => x.IsDefault && x.Id != groupId)
            .ToList()
            .Where(x => string.Equals(ExtractSingleClientType(x.ClientType), singleClientType, StringComparison.Ordinal))
            .ToList();
        foreach (var other in sameClientDefaults)
        {
            other.UnmarkDefault();
            await _groupRepo.UpdateAsync(other, ct);
        }

        target.MarkAsDefault();
        await _groupRepo.UpdateAsync(target, ct);
    }

    /// <summary>
    /// 查指定终端类型的 <c>IsDefault=1</c> 菜单组 Id（无默认组时返回 null）。
    /// 入参与库内存储的 <c>ClientType</c> 均经归一化后按单一终端类型比较。
    /// </summary>
    public Task<long?> GetDefaultGroupIdAsync(string clientType, CancellationToken ct = default)
    {
        var normalized = NormalizeClientType(clientType);
        if (normalized == null) return Task.FromResult<long?>(null);

        var match = _groupRepo.Query()
            .Where(x => x.IsDefault)
            .ToList()
            .FirstOrDefault(x => string.Equals(ExtractSingleClientType(x.ClientType), normalized, StringComparison.Ordinal));

        return Task.FromResult(match?.Id);
    }

    // ===== 统一客户端入口（Portal） =====

    /// <summary>
    /// 构建指定终端类型默认菜单组下、当前用户可见的入口树（需求 9.4–9.11）。
    /// 流程：
    /// 1. 归一化 <paramref name="clientType"/> 并定位该端 <c>IsDefault=1</c> 菜单组；无默认组时返回空入口树（需求 9.9）。
    /// 2. 读取该默认组下全部菜单项，按 <c>Order</c> 升序。
    /// 3. 判定用户身份：超管返回组下全部项（含 <c>RequireGrant=1</c>，且不论 <c>Enabled</c> 状态）（需求 9.6）；
    ///    非超管返回所有 <c>Enabled=1</c> 且（<c>RequireGrant=0</c> 或 <c>RequireGrant=1</c> 且经
    ///    <see cref="RoleMenuGroupItem"/> 授权）的项（需求 9.7）。
    /// 4. 用可见集合按 <c>ParentId</c> 组装树、同级按 <c>Order</c> 升序；父项不可见时其可见子项作为根节点保留（需求 9.5）。
    /// 雪花 Id 仍保持 <c>long</c>，由全局 JSON 配置统一序列化为字符串（需求 9.10）。
    /// 全流程读取均通过既有 <see cref="IRepository{T}.Query"/> 走既有租户隔离链路（需求 9.11）。
    /// </summary>
    public async Task<ClientPortalDto> GetClientPortalAsync(string clientType, long? userId, CancellationToken ct = default)
    {
        // 归一化终端类型，作为返回 DTO 的 ClientType（无法归一化时回退为原始入参或空字符串）
        var normalized = NormalizeClientType(clientType) ?? clientType?.Trim() ?? string.Empty;

        // 定位该端 IsDefault=1 菜单组；无默认组时返回空入口树
        // 传入已归一化的非空值（归一化对已归一化输入幂等），避免可空入参告警
        var groupId = await GetDefaultGroupIdAsync(normalized, ct);
        if (groupId == null)
        {
            return new ClientPortalDto
            {
                ClientType = normalized,
                GroupId = null,
                Items = new List<ClientPortalItemDto>()
            };
        }

        // 读取该默认组下全部菜单项，按 Order 升序（保证后续树构建时同级有序）
        var items = _itemRepo.Query()
            .Where(x => x.MenuGroupId == groupId.Value)
            .OrderBy(x => x.Order)
            .ToList();

        // 判定用户身份：解析角色集合与是否超管
        var roleIds = userId.HasValue
            ? _userRoleRepo.Query().Where(x => x.UserId == userId.Value).Select(x => x.RoleId).ToList()
            : new List<long>();
        var isSuperAdmin = roleIds.Count > 0
            && _roleRepo.Query().Any(x => roleIds.Contains(x.Id) && x.IsSuperAdmin);

        // 过滤可见集合
        List<MenuGroupItem> visible;
        if (isSuperAdmin)
        {
            // 超管：组下全部项（含 RequireGrant=1，且不论 Enabled 状态）（需求 9.6）
            visible = items;
        }
        else
        {
            // 非超管：先取该用户角色已授权的菜单项 Id 集合
            var grantedItemIds = roleIds.Count > 0
                ? _roleMenuGroupItemRepo.Query()
                    .Where(x => roleIds.Contains(x.RoleId))
                    .Select(x => x.MenuGroupItemId)
                    .ToList()
                    .ToHashSet()
                : new HashSet<long>();

            // 单项基础可见规则：Enabled=1 且（RequireGrant=0 或 RequireGrant=1 且已授权）（需求 9.7）
            bool BaseVisible(MenuGroupItem x)
                => x.Enabled && (!x.RequireGrant || grantedItemIds.Contains(x.Id));

            // 祖先级联可见：某项最终可见 ⇔ 自身基础可见 且 其所有祖先（直到根）均基础可见。
            // 目的：当某「插件根入口」（如工作流根菜单）因未授权而不可见时，
            // 其全部下级入口随之整体隐藏，绝不把子项上提为根节点单独展示——
            // 即「角色未被授予某插件首页业务入口时，列表中直接不显示该插件」。
            // 注：父项不在本组内（孤儿）的项不受祖先约束，仅按自身基础规则判定。
            var itemById = items.ToDictionary(x => x.Id);
            var effectiveCache = new Dictionary<long, bool>();

            bool EffectiveVisible(MenuGroupItem x)
            {
                if (effectiveCache.TryGetValue(x.Id, out var cached)) return cached;
                // 防御性占位，避免极端数据出现父子环导致的无限递归
                effectiveCache[x.Id] = false;

                var result = BaseVisible(x);
                if (result && x.ParentId.HasValue && itemById.TryGetValue(x.ParentId.Value, out var parent))
                {
                    // 父项在本组内时，必须父项也最终可见，本项才可见（祖先级联）
                    result = EffectiveVisible(parent);
                }
                effectiveCache[x.Id] = result;
                return result;
            }

            visible = items.Where(EffectiveVisible).ToList();
        }

        // 用可见集合构建入口树（同级按 Order 升序；父项不可见时其可见子项作为根节点保留）
        var roots = BuildClientPortalTree(visible);

        return new ClientPortalDto
        {
            ClientType = normalized,
            GroupId = groupId,
            Items = roots
        };
    }

    // ===== 角色菜单组项（item 级）授权 =====

    /// <summary>
    /// 返回各端默认菜单组（<c>IsDefault=1</c>）下的可授权入口项，按默认组分组并以树形组织。
    /// 仅纳入各端默认组下的项（需求 8.5）；每个节点标注 <c>RequireGrant</c>，
    /// 供前端将 <c>RequireGrant=false</c> 的项标记为「公共可见、无需勾选」并禁用勾选（需求 8.6）。
    /// 所有读取均通过既有 <see cref="IRepository{T}.Query"/> 走既有租户隔离链路。
    /// </summary>
    public Task<List<GrantableMenuItemDto>> GetGrantableItemsAsync(CancellationToken ct = default)
    {
        // 查全部默认菜单组（仅默认组下的项可被授权）
        var defaultGroups = _groupRepo.Query()
            .Where(x => x.IsDefault)
            .ToList();

        var result = new List<GrantableMenuItemDto>();
        foreach (var group in defaultGroups)
        {
            // 取该默认组下的全部项，按 Order 升序，便于树构建时同级有序
            var items = _itemRepo.Query()
                .Where(x => x.MenuGroupId == group.Id)
                .OrderBy(x => x.Order)
                .ToList();

            var nodes = items.Select(x => new GrantableItemNodeDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Title = x.Title,
                Icon = x.Icon,
                RequireGrant = x.RequireGrant,
                Module = x.Module,
                Order = x.Order
            }).ToList();

            result.Add(new GrantableMenuItemDto
            {
                // ClientType 取自所属默认组的 ClientType
                ClientType = group.ClientType ?? string.Empty,
                GroupId = group.Id,
                GroupName = group.Name,
                Items = BuildGrantableTree(nodes)
            });
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// 返回指定角色已授权的菜单组项 Id 集合（需求 8.4）。
    /// </summary>
    public Task<List<long>> GetRoleMenuGroupItemIdsAsync(long roleId, CancellationToken ct = default)
    {
        return Task.FromResult(_roleMenuGroupItemRepo.Query()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.MenuGroupItemId)
            .ToList());
    }

    /// <summary>
    /// 以提交集合去重后全量覆盖指定角色的菜单组项授权（需求 8.3）：
    /// 先删除该角色既有的全部 <see cref="RoleMenuGroupItem"/>，再按去重后的提交集合逐条新增，
    /// 从而保证 <c>(RoleId, MenuGroupItemId)</c> 唯一（需求 8.2）。
    /// </summary>
    public async Task SetRoleMenuGroupItemsAsync(SetRoleMenuGroupItemsInput input, CancellationToken ct = default)
    {
        // 删除该角色既有的全部 item 级授权关系
        var existing = _roleMenuGroupItemRepo.Query().Where(x => x.RoleId == input.RoleId).ToList();
        foreach (var e in existing)
        {
            await _roleMenuGroupItemRepo.DeleteAsync(e.Id, ct);
        }

        // 按去重后的提交集合逐条新增，保证 (RoleId, MenuGroupItemId) 唯一
        foreach (var itemId in input.MenuGroupItemIds.Distinct())
        {
            var entity = new RoleMenuGroupItem
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                RoleId = input.RoleId,
                MenuGroupItemId = itemId,
                CreatedAt = DateTime.Now
            };
            await _roleMenuGroupItemRepo.AddAsync(entity, ct);
        }
    }

    // ===== 安装链路客户端入口注入 / 清理 =====

    /// <summary>
    /// 供安装链路注入入口项：定位该 <paramref name="clientType"/> 端的 <c>IsDefault=1</c> 菜单组，
    /// 按入口标识 <c>(MenuGroupId, Module, Url)</c> 对每个 <see cref="ClientMenuItemSpec"/> 执行 upsert：
    /// 已存在则更新字段、不存在则新增；写入 <c>Module=moduleId</c>、<c>Title</c>、<c>Icon</c>、
    /// <c>Url=path</c>、<c>RequireGrant</c>、<c>Order</c>、<c>Badge</c>、<c>LinkType='Custom'</c>。
    /// 该端无默认菜单组时不创建任何项（直接返回）。
    /// 所有读写均通过既有 <see cref="IRepository{T}.Query"/> 走既有租户隔离链路。
    /// </summary>
    public async Task UpsertClientMenuItemsAsync(string clientType, string moduleId, IEnumerable<ClientMenuItemSpec> items, CancellationToken ct = default)
    {
        if (items == null) return;

        // 定位该端 IsDefault=1 菜单组；无默认组时不创建任何项
        var groupId = await GetDefaultGroupIdAsync(clientType, ct);
        if (groupId == null) return;

        // Module 区分大小写，仅做去空白处理与实体一致
        var normalizedModule = string.IsNullOrWhiteSpace(moduleId) ? "sys" : moduleId.Trim();

        // 预取该组下属于该模块的现有项，按入口标识 (MenuGroupId, Module, Url) 匹配 upsert
        var existingItems = _itemRepo.Query()
            .Where(x => x.MenuGroupId == groupId.Value && x.Module == normalizedModule)
            .ToList();

        // 第一遍：按入口标识 (MenuGroupId, Module, Url) upsert 每个入口项的展示字段，
        // 同时建立「path → 实体」索引，供第二遍解析父子层级（父子可在同批次任意顺序声明）。
        var byPath = new Dictionary<string, MenuGroupItem>(StringComparer.Ordinal);
        // 记录每个入口项声明的 ParentPath，第一遍不立即写父子（父项可能尚未创建），第二遍统一解析。
        var parentPathOf = new Dictionary<long, string?>();
        foreach (var x in existingItems)
        {
            if (!string.IsNullOrEmpty(x.Url) && !byPath.ContainsKey(x.Url))
            {
                byPath[x.Url] = x;
            }
        }

        foreach (var spec in items)
        {
            if (spec == null) continue;

            var path = spec.Path?.Trim();

            // 入口标识：同一菜单组、同一模块、同一 Url(path) 视为同一入口
            var existing = existingItems.FirstOrDefault(x => string.Equals(x.Url, path, StringComparison.Ordinal));
            if (existing != null)
            {
                // 更新既有入口项字段
                existing.Title = spec.Title?.Trim() ?? string.Empty;
                existing.Icon = spec.Icon?.Trim();
                existing.LinkType = "Custom";
                existing.Url = path;
                existing.Module = normalizedModule;
                existing.RequireGrant = spec.RequireGrant;
                existing.Badge = spec.Badge?.Trim();
                existing.SetOrder(spec.Order);
                await _itemRepo.UpdateAsync(existing, ct);
                if (!string.IsNullOrEmpty(path)) byPath[path!] = existing;
                parentPathOf[existing.Id] = string.IsNullOrWhiteSpace(spec.ParentPath) ? null : spec.ParentPath!.Trim();
            }
            else
            {
                // 新增入口项
                var entity = MenuGroupItem.Create(
                    menuGroupId: groupId.Value,
                    title: spec.Title ?? string.Empty,
                    linkType: "Custom",
                    url: path,
                    module: normalizedModule,
                    requireGrant: spec.RequireGrant);
                entity.Icon = spec.Icon?.Trim();
                entity.Badge = spec.Badge?.Trim();
                entity.SetOrder(spec.Order);
                await _itemRepo.AddAsync(entity, ct);

                // 加入本地缓存，避免同批次内重复 path 触发二次新增
                existingItems.Add(entity);
                if (!string.IsNullOrEmpty(path)) byPath[path!] = entity;
                parentPathOf[entity.Id] = string.IsNullOrWhiteSpace(spec.ParentPath) ? null : spec.ParentPath!.Trim();
            }
        }

        // 第二遍：解析层级。按声明的 ParentPath 在同组同模块入口中定位父项并写 ParentId；
        // 顶级入口（ParentPath 为空）或父项不存在时归一化为根（ParentId=null）。仅在层级发生变化时更新。
        foreach (var kv in parentPathOf)
        {
            var entity = existingItems.FirstOrDefault(x => x.Id == kv.Key);
            if (entity == null) continue;

            long? newParentId = null;
            var parentPath = kv.Value;
            if (!string.IsNullOrEmpty(parentPath)
                && byPath.TryGetValue(parentPath!, out var parent)
                && parent.Id != entity.Id) // 防御：避免自引用
            {
                newParentId = parent.Id;
            }

            if (entity.ParentId != newParentId)
            {
                entity.MoveTo(newParentId);
                await _itemRepo.UpdateAsync(entity, ct);
            }
        }
    }

    /// <summary>
    /// 按模块归属（<c>Module</c>）过滤查询菜单组项，恰好返回 <c>Module</c> 等于给定值的项集合。
    /// 区分大小写匹配；通过既有 <see cref="IRepository{T}.Query"/> 走既有租户隔离链路。
    /// </summary>
    public Task<List<MenuGroupItemDto>> GetItemsByModuleAsync(string module, CancellationToken ct = default)
    {
        var items = _itemRepo.Query()
            .Where(x => x.Module == module)
            .OrderBy(x => x.Order)
            .ToList();

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
            PermissionCode = x.PermissionCode,
            CssClass = x.CssClass,
            Badge = x.Badge,
            BadgeType = x.BadgeType,
            ExtraData = x.ExtraData,
            Order = x.Order,
            Enabled = x.Enabled,
            Module = x.Module,
            RequireGrant = x.RequireGrant
        }).ToList();

        return Task.FromResult(dtos);
    }

    /// <summary>
    /// 供卸载链路按模块归属（<c>Module</c>）清理客户端入口项：
    /// 查 <c>Module=moduleId</c> 的全部 <see cref="MenuGroupItem"/> Id 集合，
    /// 先级联删除其 <see cref="RoleMenuGroupItem"/> 授权关联，再删除这些 <see cref="MenuGroupItem"/>。
    /// 区分大小写匹配；不触碰 <c>Module='sys'</c> 的主框架项，且不删除任何 <see cref="MenuGroup"/> 记录。
    /// 所有读写均通过既有 <see cref="IRepository{T}.Query"/> 走既有租户隔离链路。
    /// </summary>
    public async Task RemoveClientMenuItemsByModuleAsync(string moduleId, CancellationToken ct = default)
    {
        // 防御性保护：模块标识为空或为主框架归属（sys）时不执行任何清理，避免误删主框架入口项
        if (string.IsNullOrWhiteSpace(moduleId)) return;
        var normalizedModule = moduleId.Trim();
        if (string.Equals(normalizedModule, "sys", StringComparison.Ordinal)) return;

        // 查该模块的全部菜单组项 Id（区分大小写匹配）
        var itemIds = _itemRepo.Query()
            .Where(x => x.Module == normalizedModule)
            .Select(x => x.Id)
            .ToList();
        if (itemIds.Count == 0) return;

        // 先级联删除这些菜单组项的角色授权关联（RoleMenuGroupItem）
        var roleItemIds = _roleMenuGroupItemRepo.Query()
            .Where(x => itemIds.Contains(x.MenuGroupItemId))
            .Select(x => x.Id)
            .ToList();
        if (roleItemIds.Count > 0)
        {
            await _roleMenuGroupItemRepo.DeleteRangeAsync(roleItemIds, ct);
        }

        // 再删除这些菜单组项（不删除任何 MenuGroup 记录）
        await _itemRepo.DeleteRangeAsync(itemIds, ct);
    }

    // ===== 辅助方法 =====

    /// <summary>
    /// 归一化终端类型：去空白并转大写后，将 <c>MOBILE</c> 映射为 <c>UNIAPP</c>，
    /// <c>WPF</c> / <c>WEB_PORTAL</c> / <c>UNIAPP</c> 原样通过；空或仅空白时返回 <c>null</c>。
    /// 供控制器、portal 入口查询与默认组匹配复用对外入参的统一归一化约定。
    /// </summary>
    private static string? NormalizeClientType(string? clientType)
    {
        if (string.IsNullOrWhiteSpace(clientType)) return null;
        var upper = clientType.Trim().ToUpperInvariant();
        return upper switch
        {
            "MOBILE" => "UNIAPP",
            _ => upper
        };
    }

    /// <summary>
    /// 从菜单组的 <c>ClientType</c> 字段提取单一终端类型：按逗号拆分并去空白后，
    /// 仅当恰好包含一个非空终端类型时返回其归一化值；为空或含多个终端类型（逗号分隔）时返回 <c>null</c>。
    /// 用于默认菜单组「每端单一终端类型」约束校验与同端匹配。
    /// </summary>
    private static string? ExtractSingleClientType(string? clientType)
    {
        if (string.IsNullOrWhiteSpace(clientType)) return null;
        var segments = clientType
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 1) return null;
        return NormalizeClientType(segments[0]);
    }

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

    /// <summary>
    /// 由可见的菜单项集合构建统一客户端入口树（需求 9.5）。
    /// 仅可见集合内的节点参与组装：当某项的 <c>ParentId</c> 为空、或其父项不在可见集合中时，
    /// 该项作为根节点保留（父项不可见时其可见子项上提为根）。
    /// 入参须已按 <c>Order</c> 升序，以保证根节点与各级子节点同级有序。
    /// </summary>
    private static List<ClientPortalItemDto> BuildClientPortalTree(List<MenuGroupItem> visibleItems)
    {
        // 先按可见集合内的 Id 建立映射，便于判断某项父节点是否同样可见
        var dtoMap = new Dictionary<long, ClientPortalItemDto>(visibleItems.Count);
        var dtos = new List<ClientPortalItemDto>(visibleItems.Count);
        foreach (var item in visibleItems)
        {
            var dto = new ClientPortalItemDto
            {
                Id = item.Id,
                ParentId = item.ParentId,
                Title = item.Title,
                Icon = item.Icon,
                Url = item.Url,
                Badge = item.Badge,
                BadgeType = item.BadgeType,
                Order = item.Order,
                RequireGrant = item.RequireGrant,
                Module = item.Module
            };
            dtoMap[item.Id] = dto;
            dtos.Add(dto);
        }

        var roots = new List<ClientPortalItemDto>();
        foreach (var dto in dtos)
        {
            // 父项存在且同样可见时挂到父节点下，否则作为根节点保留
            if (dto.ParentId.HasValue && dtoMap.TryGetValue(dto.ParentId.Value, out var parent))
            {
                parent.Children ??= new List<ClientPortalItemDto>();
                parent.Children.Add(dto);
            }
            else
            {
                roots.Add(dto);
            }
        }

        return roots;
    }

    /// <summary>
    /// 平铺列表构建可授权入口树形结构；同级按 <c>Order</c> 升序排列。
    /// 入参建议已按 <c>Order</c> 升序，以保证根节点与各级子节点顺序稳定。
    /// </summary>
    private static List<GrantableItemNodeDto> BuildGrantableTree(List<GrantableItemNodeDto> nodes)
    {
        var map = nodes.ToDictionary(x => x.Id);
        var roots = new List<GrantableItemNodeDto>();

        foreach (var node in nodes)
        {
            if (node.ParentId.HasValue && map.TryGetValue(node.ParentId.Value, out var parent))
            {
                parent.Children ??= new List<GrantableItemNodeDto>();
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
