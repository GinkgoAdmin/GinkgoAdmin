// 文件功能说明：
// 菜单应用服务的基础空实现，后续将填充具体业务逻辑。

using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
// using Ginkgo.Domain.Permissions; // 已移除独立权限表依赖
using Ginkgo.Domain.Roles;
using Ginkgo.Shared;

namespace Ginkgo.Application.Menus;

/// <summary>
/// 菜单应用服务实现。
/// </summary>
public sealed class MenuAppService : IMenuAppService
{
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<RolePermission> _rolePermRepository;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="menuRepository">菜单仓储。</param>
    /// <param name="rolePermRepository">角色-菜单授权关系仓储。</param>
    public MenuAppService(IRepository<Menu> menuRepository,
        IRepository<RolePermission> rolePermRepository)
    {
        _menuRepository = menuRepository;
        _rolePermRepository = rolePermRepository;
    }

    /// <inheritdoc />
    public async Task<PagedResult<MenuListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;

        var baseQuery = _menuRepository.Query();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            baseQuery = baseQuery.Where(x => x.Name.Contains(k) || x.Route!.Contains(k) || x.Code!.Contains(k));
        }
        var total = baseQuery.LongCount();
        var items = baseQuery
            .OrderBy(x => x.ParentId).ThenBy(x => x.Order)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new MenuListItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Route = x.Route ?? string.Empty,
                Type = x.Type,
                Icon = x.Icon,
                SupportedClients = x.SupportedClients,
                WebUrl = x.WebUrl,
                MobileUrl = x.MobileUrl,
                WpfRouteUrl = x.WpfRouteUrl,
                WebRouteUrl = x.WebRouteUrl,
                MobileRouteUrl = x.MobileRouteUrl
            })
            .ToList();
        return await Task.FromResult(new PagedResult<MenuListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        });
    }

    /// <inheritdoc />
    public async Task<MenuDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return null;
        return new MenuDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Route = entity.Route ?? string.Empty,
            Type = entity.Type,
            ItemMode = entity.ItemMode,
            Icon = entity.Icon,
            // 多客户端与显示模式
            SupportedClients = entity.SupportedClients,
            WebUrl = entity.WebUrl,
            MobileUrl = entity.MobileUrl,
            // 每客户端显示模式
            WpfDisplayMode = entity.WpfDisplayMode,
            WebDisplayMode = entity.WebDisplayMode,
            MobileDisplayMode = entity.MobileDisplayMode,
            // 每客户端最终地址
            WpfRouteUrl = entity.WpfRouteUrl,
            WebRouteUrl = entity.WebRouteUrl,
            MobileRouteUrl = entity.MobileRouteUrl,
            Url = entity.Url,
            ParentId = entity.ParentId,
            Order = entity.Order,
            Enabled = entity.Enabled,
            Code = entity.Code,
            Resource = entity.Resource,
            Method = entity.Method
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CreateMenuInput input, CancellationToken cancellationToken = default)
    {
        // 唯一性校验：Name + Parent 同层不可重名；Route 全局唯一（非空时）
        if (!string.IsNullOrWhiteSpace(input.Route))
        {
            var existsRoute = _menuRepository.Query().Any(x => x.Route == input.Route);
            if (existsRoute) throw new InvalidOperationException($"路由已存在: {input.Route}");
        }
        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            var existsName = _menuRepository.Query().Any(x => x.ParentId == input.ParentId && x.Name == input.Name);
            if (existsName) throw new InvalidOperationException($"同层名称已存在: {input.Name}");
        }
        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var existsCode = _menuRepository.Query().Any(x => x.Code == input.Code);
            if (existsCode) throw new InvalidOperationException($"编码已存在: {input.Code}");
        }
        var entity = Menu.Create(
            name: input.Name,
            type: input.Type,
            route: input.Route,
            parentId: input.ParentId,
            icon: input.Icon,
            url: input.Url,
            code: input.Code,
            supportedClients: input.SupportedClients,
            webUrl: input.WebUrl,
            mobileUrl: input.MobileUrl,
            wpfDisplayMode: input.WpfDisplayMode,
            webDisplayMode: input.WebDisplayMode,
            mobileDisplayMode: input.MobileDisplayMode,
            wpfRouteUrl: input.WpfRouteUrl,
            webRouteUrl: input.WebRouteUrl,
            mobileRouteUrl: input.MobileRouteUrl
        );
        // 其余元数据
        entity.ItemMode = input.ItemMode;
        entity.Resource = string.IsNullOrWhiteSpace(input.Resource) ? null : input.Resource.Trim();
        entity.Method = string.IsNullOrWhiteSpace(input.Method) ? null : input.Method.Trim().ToUpperInvariant();
        await _menuRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, UpdateMenuInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return;
        if (!string.IsNullOrWhiteSpace(input.Route) && !string.Equals(entity.Route, input.Route, StringComparison.OrdinalIgnoreCase))
        {
            var existsRoute = _menuRepository.Query().Any(x => x.Id != id && x.Route == input.Route);
            if (existsRoute) throw new InvalidOperationException($"路由已存在: {input.Route}");
        }
        if (!string.IsNullOrWhiteSpace(input.Name) && !string.Equals(entity.Name, input.Name, StringComparison.OrdinalIgnoreCase))
        {
            var existsName = _menuRepository.Query().Any(x => x.Id != id && x.ParentId == entity.ParentId && x.Name == input.Name);
            if (existsName) throw new InvalidOperationException($"同层名称已存在: {input.Name}");
        }
        if (!string.IsNullOrWhiteSpace(input.Code) && !string.Equals(entity.Code, input.Code, StringComparison.OrdinalIgnoreCase))
        {
            var existsCode = _menuRepository.Query().Any(x => x.Id != id && x.Code == input.Code);
            if (existsCode) throw new InvalidOperationException($"编码已存在: {input.Code}");
        }
        entity.UpdateMeta(input.Name, input.Route, input.Icon, input.Url, input.Code,
                          input.SupportedClients, input.WebUrl, input.MobileUrl,
                          input.WpfDisplayMode, input.WebDisplayMode, input.MobileDisplayMode,
                          input.WpfRouteUrl, input.WebRouteUrl, input.MobileRouteUrl);
        entity.SetType(input.Type);
        entity.ItemMode = input.ItemMode;
        entity.Resource = string.IsNullOrWhiteSpace(input.Resource) ? null : input.Resource.Trim();
        entity.Method = string.IsNullOrWhiteSpace(input.Method) ? null : input.Method.Trim().ToUpperInvariant();
        entity.SetOrder(input.Order);
        if (input.Enabled) entity.Enable(); else entity.Disable();
        await _menuRepository.UpdateAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return;

        // 递归删除所有下级菜单（深度优先，先删子孙再删自身）
        var children = _menuRepository.Query().Where(x => x.ParentId == id).Select(x => x.Id).ToList();
        foreach (var childId in children)
        {
            await DeleteAsync(childId, cancellationToken);
        }

        // 删除与该菜单绑定的角色授权关系（PermissionId 存储 Menu.Id）
        var rolePerms = _rolePermRepository.Query().Where(rp => rp.PermissionId == entity.Id).ToList();
        foreach (var rp in rolePerms)
        {
            await _rolePermRepository.DeleteAsync(rp.Id, cancellationToken);
        }
        await _menuRepository.DeleteAsync(id, cancellationToken);
    }
    // 旧的独立权限表同步逻辑已彻底移除（以菜单 + ginkgo_Sys_RolePermission 为唯一授权来源）

    // 辅助：从路由推断模块/动作以及 API 资源模板（用于设置 Button 权限的 Resource/Method）
    private static string? GetModuleFromRoute(string? route)
        => string.IsNullOrWhiteSpace(route) ? null : route.TrimEnd('/').Split('/').LastOrDefault(s => !string.IsNullOrWhiteSpace(s));

    private static string? GetActionFromButton(Menu button, string? parentRoute)
    {
        // 优先从按钮 Route 提取 :action
        var code = button.Route;
        if (!string.IsNullOrWhiteSpace(code))
        {
            var idx = code.LastIndexOf(':');
            if (idx >= 0 && idx < code.Length - 1) return code[(idx + 1)..].ToLowerInvariant();
        }
        // 其次根据按钮名称关键词推断
        var name = (button.Name ?? string.Empty).ToLowerInvariant();
        if (name.Contains("新增") || name.Contains("添加") || name.Contains("新建") || name.Contains("add") || name.Contains("create")) return "add";
        if (name.Contains("修改") || name.Contains("编辑") || name.Contains("update") || name.Contains("edit")) return "edit";
        if (name.Contains("删除") || name.Contains("移除") || name.Contains("del") || name.Contains("remove") || name.Contains("delete")) return "delete";
        if (name.Contains("查看") || name.Contains("详情") || name.Contains("view") || name.Contains("detail")) return "view";
        if (name.Contains("查询") || name.Contains("搜索") || name.Contains("列表") || name.Contains("list") || name.Contains("search") || name.Contains("query")) return "list";
        return null; // 无法识别则跳过（容错）
    }

    private static (string method, string resourceTpl, string cn) MapApiByAction(string module, string action)
    {
        var raw = (action ?? string.Empty).Trim('/');
        var lower = raw.ToLowerInvariant();
        string method = "POST"; // 默认：其他 → 统一走 POST
        string cn = "操作";

        // 标准 CRUD
        if (lower is "add" or "create") { method = "POST"; cn = "新增"; return (method, $"/api/v1/{module}", cn); }
        if (lower is "edit" or "update") { method = "PUT"; cn = "修改"; return (method, $"/api/v1/{module}/{{id}}", cn); }
        if (lower is "delete" or "del" or "remove") { method = "DELETE"; cn = "删除"; return (method, $"/api/v1/{module}/{{id}}", cn); }
        if (lower is "view" or "detail" or "get") { method = "GET"; cn = "查看"; return (method, $"/api/v1/{module}/{{id}}", cn); }

        // 其余：按规则映射
        // 允许 raw 中包含子路径和 {id} 占位：如 "{id}/permissions"、"permissions/tree"
        string resource = $"/api/v1/{module}/{raw}";

        // GET 场景关键词
        if (lower.StartsWith("get/") || lower.StartsWith("list") || lower.StartsWith("query") || lower.Contains("tree") || lower.Contains("export") || lower.StartsWith("download"))
        {
            method = "GET";
            cn = lower.Contains("tree") ? "查询树" : "查询";
        }

        // PUT/DELETE 场景关键词（若路径中未包含任何占位符 {...} 则默认补 /{id}）
        if (lower.StartsWith("update/") || lower.StartsWith("put/"))
        {
            method = "PUT"; cn = "修改";
            if (!resource.Contains('{')) resource += "/{id}";
        }
        if (lower.StartsWith("delete/") || lower.StartsWith("remove/"))
        {
            method = "DELETE"; cn = "删除";
            if (!resource.Contains('{')) resource += "/{id}";
        }

        return (method, resource, cn);
    }

    // 之前的“自动生成独立 API 权限”逻辑已移除，改为仅创建 Button 类型权限，Code=完整菜单地址:动作
}

