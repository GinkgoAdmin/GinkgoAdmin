// 文件功能说明：
// 菜单模块 API 控制器，占位实现。

using Ginkgo.Application.Menus;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Roles;
using Ginkgo.Api.Modules;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 菜单接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/menus")] 
[Authorize(Policy = "Permission")]
[ApiVersion("1.0")]
public sealed class MenusController : ControllerBase
{
    private readonly IMenuAppService _service;
    private readonly IRepository<Menu> _menuRepo;
    private readonly IRepository<UserRole> _userRoleRepo;
    private readonly IRepository<RolePermission> _rolePermRepo;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="service">菜单应用服务。</param>
    /// <param name="menuRepo">菜单仓储。</param>
    /// <param name="userRoleRepo">用户角色仓储。</param>
    /// <param name="rolePermRepo">角色权限仓储。</param>
    /// <param name="configuration">应用配置。</param>
    public MenusController(
        IMenuAppService service,
        IRepository<Menu> menuRepo,
        IRepository<UserRole> userRoleRepo,
        IRepository<RolePermission> rolePermRepo,
        IConfiguration configuration)
    {
        _service = service;
        _menuRepo = menuRepo;
        _userRoleRepo = userRoleRepo;
        _rolePermRepo = rolePermRepo;
        _configuration = configuration;
    }

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字。</param>
    [HttpGet]
    public async Task<Result<PagedResult<MenuListItemDto>>> GetAsync([FromQuery] PageRequest request, [FromQuery] string? keyword)
    {
        var data = await _service.GetPagedAsync(request, keyword);
        return Result<PagedResult<MenuListItemDto>>.Success(data);
    }

    /// <summary>
    /// 获取详情。
    /// </summary>
    /// <param name="id">菜单 Id（Snowflake ID）。</param>
    [HttpGet("{id}")]
    public async Task<Result<MenuDetailDto>> GetByIdAsync(long id)
    {
        var data = await _service.GetAsync(id);
        if (data == null) return Result<MenuDetailDto>.Fail(404, "菜单不存在");
        return Result<MenuDetailDto>.Success(data);
    }

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="input">创建输入。</param>
    [HttpPost]
    public async Task<Result<long>> CreateAsync([FromBody] CreateMenuInput input)
    {
        try
        {
            var id = await _service.CreateAsync(input);
            return Result<long>.Success(id, "创建成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result<long>.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 更新。
    /// </summary>
    /// <param name="id">菜单 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    [HttpPut("{id}")]
    public async Task<Result> UpdateAsync(long id, [FromBody] UpdateMenuInput input)
    {
        try
        {
            await _service.UpdateAsync(id, input);
            return Result.Success("更新成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 删除。
    /// </summary>
    /// <param name="id">菜单 Id（Snowflake ID）。</param>
    [HttpDelete("{id}")]
    public async Task<Result> DeleteAsync(long id)
    {
        await _service.DeleteAsync(id);
        return Result.Success("删除成功");
    }

    /// <summary>
    /// 批量删除。
    /// </summary>
    /// <param name="ids">要删除的 Id 列表。</param>
    [HttpPost("batch-delete")]
    public async Task<Result> BatchDeleteAsync([FromBody] long[] ids)
    {
        foreach (var id in ids.Distinct())
        {
            await _service.DeleteAsync(id);
        }
        return Result.Success("批量删除成功");
    }

    /// <summary>
    /// 管理端菜单树（全部，按 ParentId + Order 排序）。
    /// </summary>
    [HttpGet("tree/all")]
    public ActionResult<Result<List<MenuNodeDto>>> GetAllTree()
    {
        // 管理端需要看到所有类型（Directory/Menu/Button/Api），包括禁用项
        // 模块兼容过滤必须在 ToList 后于内存执行，SqlSugar 无法翻译自定义静态方法
        var all = ModuleDatabaseCompatibility.FilterMenusInMemory(
            _menuRepo.Query()
                .OrderBy(x => x.ParentId)
                .ThenBy(x => x.Order)
                .ToList(),
            _configuration,
            x => x.Module);
        var dict = all.ToDictionary(x => x.Id, x => new MenuNodeDto
        {
            Id = x.Id,
            Name = x.Name,
            Route = x.Route ?? string.Empty,
            Code = x.Code,
            Resource = x.Resource,
            Method = x.Method,
            Type = x.Type, // 添加类型字段
            Icon = x.Icon,
            Enabled = x.Enabled,
            Order = x.Order,
            // 多客户端字段直接随树返回，避免前端逐条读取详情
            SupportedClients = x.SupportedClients,
            WpfDisplayMode = x.WpfDisplayMode,
            WebDisplayMode = x.WebDisplayMode,
            MobileDisplayMode = x.MobileDisplayMode,
            WpfRouteUrl = x.WpfRouteUrl,
            WebRouteUrl = x.WebRouteUrl,
            MobileRouteUrl = x.MobileRouteUrl,
            Children = new List<MenuNodeDto>()
        });
        var roots = new List<MenuNodeDto>();
        foreach (var m in all)
        {
            if (m.ParentId.HasValue && dict.TryGetValue(m.ParentId.Value, out var parent))
            {
                parent.Children!.Add(dict[m.Id]);
            }
            else
            {
                roots.Add(dict[m.Id]);
            }
        }
        return Ok(Result<List<MenuNodeDto>>.Success(roots, "成功"));
    }

    /// <summary>
    /// 当前用户可见的菜单树：
    /// - ADMIN：返回所有启用的非 Api/Button 菜单
    /// - 非 ADMIN：按用户角色→角色权限映射到菜单 Id，自动补齐祖先目录
    /// </summary>
    [HttpGet("tree/my")]
    [AllowAnonymous] // 未登录返回空
    public ActionResult<Result<List<MenuNodeDto>>> GetMyMenuTree([FromQuery] string clientType)
    {
        if (string.IsNullOrWhiteSpace(clientType)) return BadRequest(Result<List<MenuNodeDto>>.Fail(400, "缺少必需参数 clientType"));
        var ct = NormalizeClientType(clientType);

        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        var isAdmin = isAuthenticated && (User?.IsInRole("ADMIN") == true);

        // 1) 全量菜单（用于祖先回溯），2) 可见集合（仅启用且非 Api/Button），并按客户端过滤
        var allMenus = ModuleDatabaseCompatibility.FilterMenusInMemory(
            _menuRepo.Query()
                .OrderBy(x => x.ParentId)
                .ThenBy(x => x.Order)
                .ToList(),
            _configuration,
            x => x.Module);
        var allMenusDict = allMenus.ToDictionary(x => x.Id, x => x);
        
        // 检查菜单及其所有祖先是否都启用（含环检测，避免 ParentId 自引用导致死循环）
        var ancestorEnabledMemo = new Dictionary<long, bool>();
        bool IsAncestorChainEnabled(Menu menu)
        {
            if (ancestorEnabledMemo.TryGetValue(menu.Id, out var cached)) return cached;
            var visiting = new HashSet<long>();
            var current = menu;
            while (current != null)
            {
                if (!visiting.Add(current.Id))
                {
                    ancestorEnabledMemo[menu.Id] = false;
                    return false;
                }
                if (!current.Enabled)
                {
                    ancestorEnabledMemo[menu.Id] = false;
                    return false;
                }
                if (current.ParentId.HasValue && allMenusDict.TryGetValue(current.ParentId.Value, out var parent))
                    current = parent;
                else
                    break;
            }
            ancestorEnabledMemo[menu.Id] = true;
            return true;
        }
        
        var visibleCandidates = allMenus
            .Where(x => x.Enabled && x.Type != "Api" && x.Type != "Button")
            .Where(x => Supports(x.SupportedClients, ct))
            .Where(x => IsAncestorChainEnabled(x)) // 父级链必须全部启用
            .ToList();

        if (!isAuthenticated)
        {
            return Ok(Result<List<MenuNodeDto>>.Success(new List<MenuNodeDto>(), "未登录"));
        }

        if (isAdmin)
        {
            return Ok(Result<List<MenuNodeDto>>.Success(BuildTree(visibleCandidates, ct), "成功"));
        }

        // 非 admin：按角色→权限→菜单可见集合，并补齐祖先
        var uid = User?.Claims?.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId))
        {
            return Ok(Result<List<MenuNodeDto>>.Success(new List<MenuNodeDto>(), "无效用户"));
        }

        var roleIds = _userRoleRepo.Query().Where(x => x.UserId == userId).Select(x => x.RoleId).Distinct().ToList();
        if (roleIds.Count == 0)
        {
            return Ok(Result<List<MenuNodeDto>>.Success(new List<MenuNodeDto>(), "无角色"));
        }

        var grantedMenuIds = new HashSet<long>(_rolePermRepo.Query()
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.PermissionId)
            .Distinct()
            .ToList());

        // 构建 Id->Menu 映射（使用全量菜单回溯祖先），再仅保留可见类型
        var fullDict = allMenus.ToDictionary(x => x.Id, x => x);
        var visibleDict = visibleCandidates.ToDictionary(x => x.Id, x => x);
        var visibleIds = new HashSet<long>();
        foreach (var id in grantedMenuIds)
        {
            // 从全量菜单开始定位（即便授予的是 Api/Button，也可向上回溯）
            if (!fullDict.TryGetValue(id, out var node)) continue;
            // 自身可见则加入
            if (visibleDict.ContainsKey(node.Id)) visibleIds.Add(node.Id);
            // 回溯祖先链，遇到可见类型则加入（含环检测）
            var visited = new HashSet<long> { node.Id };
            var p = node.ParentId;
            while (p.HasValue && visited.Add(p.Value) && fullDict.TryGetValue(p.Value, out var parent))
            {
                if (visibleDict.ContainsKey(parent.Id)) visibleIds.Add(parent.Id);
                p = parent.ParentId;
            }
        }

        var filtered = visibleCandidates.Where(x => visibleIds.Contains(x.Id)).ToList();
        return Ok(Result<List<MenuNodeDto>>.Success(BuildTree(filtered, ct), "成功"));
    }

    /// <summary>
    /// 当前用户“按钮权限代码”列表（用于客户端控制按钮显示）。
    /// 规则：
    /// - ADMIN：返回所有启用的 Button 类型菜单的 Route（如 /system/menus:add）
    /// - 非 ADMIN：根据用户角色→角色权限，筛选启用的 Button 类型菜单 Route
    /// </summary>
    [HttpGet("my/buttons")]
    [AllowAnonymous] // 仅返回当前登录用户的按钮码；未登录返回空
    public ActionResult<Result<List<string>>> GetMyButtonCodes()
    {
        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        if (!isAuthenticated)
        {
            return Ok(Result<List<string>>.Success(new List<string>(), "未登录"));
        }

        var isAdmin = User?.IsInRole("ADMIN") == true;
        if (isAdmin)
        {
            // 避免在 IQueryable 中使用 string.IsNullOrEmpty 由 ORM 翻译引发异常，改为内存中过滤
            var allRoutes = _menuRepo.Query()
                .Where(m => m.Enabled && m.Type == "Button")
                .Select(m => m.Route)
                .ToList();
            var all = allRoutes.Where(r => !string.IsNullOrEmpty(r)).Select(r => r!).ToList();
            return Ok(Result<List<string>>.Success(all, "成功"));
        }

        var uid = User?.Claims?.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Ok(Result<List<string>>.Success(new List<string>(), "无效用户"));

        var roleIds = _userRoleRepo.Query().Where(x => x.UserId == userId).Select(x => x.RoleId).Distinct().ToList();
        if (roleIds.Count == 0) return Ok(Result<List<string>>.Success(new List<string>(), "无角色"));

        var grantedMenuIds = new HashSet<long>(_rolePermRepo.Query()
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.PermissionId)
            .Distinct()
            .ToList());

        // 避免 ORM 翻译 string.IsNullOrEmpty，先查询 Id+Route 再在内存中过滤
        var buttons = _menuRepo.Query()
            .Where(m => m.Enabled && m.Type == "Button")
            .Select(m => new { m.Id, m.Route })
            .ToList();

        var allowed = buttons
            .Where(b => !string.IsNullOrEmpty(b.Route) && grantedMenuIds.Contains(b.Id))
            .Select(b => b.Route!)
            .ToList();
        return Ok(Result<List<string>>.Success(allowed, "成功"));
    }

    private static List<MenuNodeDto> BuildTree(List<Menu> menus, string clientType)
    {
        var dict = menus.ToDictionary(x => x.Id, x => new MenuNodeDto
        {
            Id = x.Id,
            Name = x.Name,
            // 按数据库原样直出，不做客户端替换/拼接
            Route = x.Route ?? string.Empty,
            Code = x.Code,
            Resource = x.Resource,
            Method = x.Method,
            Type = x.Type,
            Icon = x.Icon,
            Enabled = x.Enabled,
            Order = x.Order,
            // 多端字段也一并直出，保持与数据库一致
            SupportedClients = x.SupportedClients,
            WpfDisplayMode = x.WpfDisplayMode,
            WebDisplayMode = x.WebDisplayMode,
            MobileDisplayMode = x.MobileDisplayMode,
            WpfRouteUrl = x.WpfRouteUrl,
            WebRouteUrl = x.WebRouteUrl,
            MobileRouteUrl = x.MobileRouteUrl,
            Children = new List<MenuNodeDto>()
        });
        var roots = new List<MenuNodeDto>();
        foreach (var m in menus)
        {
            if (m.ParentId.HasValue && dict.TryGetValue(m.ParentId.Value, out var parent))
            {
                parent.Children!.Add(dict[m.Id]);
            }
            else
            {
                roots.Add(dict[m.Id]);
            }
        }
        return roots;
    }

    private static string NormalizeClientType(string clientType)
    {
        var ct = clientType?.Trim().ToUpperInvariant();
        return ct switch { "WPF" => "WPF", "WEB" => "WEB", "MOBILE" => "MOBILE", _ => "WEB" };
    }

    private static bool Supports(string? supportedClients, string clientType)
    {
        if (string.IsNullOrWhiteSpace(clientType)) return false;
        // 如果 supportedClients 为空，默认支持所有客户端
        if (string.IsNullOrWhiteSpace(supportedClients)) return true;
        var tokens = supportedClients.Split(',').Select(t => t.Trim().ToUpperInvariant());
        return tokens.Contains(clientType);
    }

    // 保留原始路由直出：客户端具体映射由前端完成
}

/// <summary>
/// 菜单树节点 DTO。
/// </summary>
public sealed class MenuNodeDto
{
    /// <summary>标识（Snowflake ID）。</summary>
    public long Id { get; set; }
    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>路由/标识。</summary>
    public string Route { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Resource { get; set; }
    public string? Method { get; set; }
    /// <summary>类型。</summary>
    public string Type { get; set; } = "Menu";
    /// <summary>图标。</summary>
    public string? Icon { get; set; }
    public bool Enabled { get; set; }
    public int Order { get; set; }

    // 多客户端字段（为提高列表性能，随树接口直接下发）
    public string? SupportedClients { get; set; }
    public string? WpfDisplayMode { get; set; }
    public string? WebDisplayMode { get; set; }
    public string? MobileDisplayMode { get; set; }
    public string? WpfRouteUrl { get; set; }
    public string? WebRouteUrl { get; set; }
    public string? MobileRouteUrl { get; set; }

    /// <summary>子节点。</summary>
    public List<MenuNodeDto>? Children { get; set; }
}
