using System.Security.Claims;
using Ginkgo.Domain;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Menus;
using Ginkgo.Plugin.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Ginkgo.Api.Auth;

public sealed class PermissionRequirement : IAuthorizationRequirement { }

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRepository<RolePermission> _rolePermRepo;
    private readonly IRepository<Ginkgo.Domain.Users.UserRole> _userRoleRepo;
    private readonly IRepository<Menu> _menuRepo;
    private readonly IRepository<Role> _roleRepo;
    private readonly IMemoryCache _cache;
    private readonly PermissionCacheInvalidator _cacheInvalidator;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public PermissionAuthorizationHandler(
        IRepository<RolePermission> rolePermRepo,
        IRepository<Ginkgo.Domain.Users.UserRole> userRoleRepo,
        IRepository<Menu> menuRepo,
        IRepository<Role> roleRepo,
        IMemoryCache cache,
        PermissionCacheInvalidator cacheInvalidator)
    {
        _rolePermRepo = rolePermRepo;
        _userRoleRepo = userRoleRepo;
        _menuRepo = menuRepo;
        _roleRepo = roleRepo;
        _cache = cache;
        _cacheInvalidator = cacheInvalidator;
    }

    // 仅登录即可访问的接口精确白名单：(HTTP方法, 归一化路径)。
    // 归一化路径由 NormalizePath 生成：全小写 + 数字/Guid 段替换为 {id}。
    // 放入该集合的接口语义应严格限定为"当前登录用户操作自己数据 / 纯 lookup 下拉数据"，
    // 管理类接口必须走菜单表 Resource+Method 授权，不得加入本表。
    private static readonly HashSet<(string Method, string Path)> _loginOnlyExact =
        new()
        {
            // 文件：当前用户自己的文件操作（方法内部已有所有权校验）
            ("POST",   "/api/v1/files/upload"),
            ("GET",    "/api/v1/files"),
            ("GET",    "/api/v1/files/{id}"),
            ("GET",    "/api/v1/files/{id}/content"),
            ("GET",    "/api/v1/files/{id}/download"),
            ("GET",    "/api/v1/files/ticket/sign"),
            ("DELETE", "/api/v1/files/{id}"),

            // 消息通知：当前用户查看/操作自己的消息
            ("GET",    "/api/message/list"),
            ("GET",    "/api/message/unread-count"),
            ("GET",    "/api/message/{id}"),
            ("PUT",    "/api/message/{id}/read"),
            ("PUT",    "/api/message/read-all"),

            // 系统通知：当前用户自己的通知
            ("GET",    "/api/v1/notifications/my/unread-count"),
            ("GET",    "/api/v1/notifications/my/{id}"),
            ("POST",   "/api/v1/notifications/my/{id}/read"),
            ("GET",    "/api/v1/notifications/{id}/attachments"),
            ("GET",    "/api/v1/notifications/{id}/attachments/{id}/download"),

            // lookup / 下拉数据：多个管理页面都会加载，登录即可读
            ("GET",    "/api/v1/roles/tree"),
            ("GET",    "/api/v1/roles/permissions/tree"),
            ("GET",    "/api/v1/dictionaries"),
            ("GET",    "/api/v1/dictionaries/categories"),
            ("GET",    "/api/v1/dictionaries/items"),
            ("GET",    "/api/v1/menus/my"),
        };

    // 仅登录即可访问的"路径前缀"白名单：仅保留少量业务网关类前缀。
    // 现存保留项：DevScaffold 脚手架（方法内部再由 IsDevMode 控制）。
    private static readonly string[] _loginOnlyPrefixes = new[]
    {
        "/api/devscaffold/",
    };

    /// <summary>
    /// 判断路径+方法是否落入"仅登录"白名单。
    /// 输入的 normalizedPath 必须已经过 NormalizePath 归一化（小写 + {id} 替换）。
    /// </summary>
    private static bool IsLoginOnlyPath(string normalizedPath, string method)
    {
        if (_loginOnlyExact.Contains((method, normalizedPath)))
            return true;
        foreach (var prefix in _loginOnlyPrefixes)
        {
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // 从当前请求推断"资源权限"：匹配菜单表中的 Resource + Method
        if (!TryGetHttp(context, out var http)) return Task.CompletedTask; // 无上下文时不拦截

        // 检查端点是否标记了 [AllowAnonymous]，如果是则直接通过
        // 这是为了支持动态加载的模块控制器中的 [AllowAnonymous] 属性
        var endpoint = http.GetEndpoint();
        if (endpoint != null)
        {
            var allowAnonymous = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>();
            if (allowAnonymous != null)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        var method = http.Request.Method?.ToUpperInvariant() ?? "GET";
        var path = http.Request.Path.ToString();
        var norm = NormalizePath(path);

        // 文档门户接口 - 完全公开（权限由控制器内部按产品设置判断）
        if (path.StartsWith("/api/docs/portal/", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 解析当前登录用户 Id
        var uidClaim = context.User?.Claims?.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        long.TryParse(uidClaim, out var userId);
        var isAuthenticated = userId > 0;

        // 超管直接通过：兼容旧的字符串角色 "ADMIN"，以及基于 Role.IsSuperAdmin 字段的现代机制
        if (isAuthenticated && (context.User?.IsInRole("ADMIN") == true || IsSuperAdminUser(userId)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 检查是否是"登录即可访问"的白名单接口
        if (IsLoginOnlyPath(norm, method))
        {
            if (isAuthenticated)
            {
                context.Succeed(requirement);
            }
            // 未登录 => 保持未授权，交由框架 401/403 处理
            return Task.CompletedTask;
        }

        // 检查端点是否标记了 [LoginOnly] 特性 —— 插件可在自己的控制器上声明，无需修改主框架白名单
        if (endpoint?.Metadata.GetMetadata<LoginOnlyAttribute>() != null)
        {
            if (isAuthenticated)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }

        // 未登录访问需权限的资源：不放行
        if (!isAuthenticated)
        {
            return Task.CompletedTask;
        }

        // 在菜单表中查找匹配的资源（启用项，非目录）— 缓存 30 秒
        var candidates = _cache.GetOrCreate(_cacheInvalidator.MenuCandidatesCacheKey, entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            return _menuRepo.Query()
                .Where(m => m.Enabled && m.Type != "Directory" && m.Resource != null && m.Method != null)
                .ToList();
        }) ?? new List<Menu>();
        var matchedCandidates = candidates.Where(m =>
            string.Equals(m.Method, method, StringComparison.OrdinalIgnoreCase) &&
            IsResourceMatch(m.Resource!, norm)).ToList();

        // 未配置资源映射：默认拒绝。要求所有 API 必须在菜单中配置 Resource+Method 才能访问
        if (matchedCandidates.Count == 0)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        // 缓存用户角色 ID 列表 — 每用户 30 秒
        var userRoleCacheKey = _cacheInvalidator.UserRolesCacheKey(userId);
        var roleIds = _cache.GetOrCreate(userRoleCacheKey, entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            return _userRoleRepo.Query().Where(x => x.UserId == userId).Select(x => x.RoleId).Distinct().ToList();
        }) ?? new List<long>();
        if (roleIds.Count == 0) return Task.CompletedTask;

        // 缓存角色权限 — 按角色 ID 组合 30 秒
        var roleKey = string.Join(",", roleIds.OrderBy(x => x));
        var grantsCacheKey = _cacheInvalidator.RoleGrantsCacheKey(roleKey);
        var grantedMenuIds = _cache.GetOrCreate(grantsCacheKey, entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            return new HashSet<long>(_rolePermRepo.Query().Where(x => roleIds.Contains(x.RoleId)).Select(x => x.PermissionId).Distinct().ToList());
        }) ?? new HashSet<long>();
        
        // 检查匹配的菜单本身或其任意祖先是否在授权列表中
        // 这样只要用户勾选了父级菜单（如"角色管理"），其下所有 Api 子节点自动拥有权限
        if (matchedCandidates.Any(matched => HasPermissionOrAncestor(matched, grantedMenuIds, candidates)))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }

    private static bool TryGetHttp(AuthorizationHandlerContext context, out HttpContext http)
    {
        if (context.Resource is HttpContext hc) { http = hc; return true; }
        if (context.Resource is Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext fc && fc.HttpContext is HttpContext h2) { http = h2; return true; }
        http = null!; return false;
    }

    /// <summary>
    /// 检查匹配的菜单本身或其任意祖先是否在授权列表中。
    /// 这样只要用户勾选了父级菜单（如"角色管理"），其下所有 Api/Button 子节点自动拥有权限。
    /// </summary>
    private bool HasPermissionOrAncestor(Menu matched, HashSet<long> grantedMenuIds, List<Menu> candidates)
    {
        // 先检查自身
        if (grantedMenuIds.Contains(matched.Id)) return true;
        
        // 向上遍历祖先链
        var current = matched;
        var visited = new HashSet<long> { current.Id };
        while (current.ParentId.HasValue)
        {
            if (!visited.Add(current.ParentId.Value)) break; // 防止循环
            if (grantedMenuIds.Contains(current.ParentId.Value)) return true;
            // 从缓存的菜单候选列表中查找父级（避免额外 DB 查询）
            var parent = candidates.FirstOrDefault(m => m.Id == current.ParentId.Value)
                      ?? _menuRepo.Query().FirstOrDefault(m => m.Id == current.ParentId.Value);
            if (parent == null) break;
            current = parent;
        }
        return false;
    }

    private static string NormalizePath(string path)
    {
        var p = path.ToLowerInvariant();
        // 将 GUID 和纯数字段（Snowflake ID）归一化为 {id}
        var parts = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (Guid.TryParse(parts[i], out _)) parts[i] = "{id}";
            else if (long.TryParse(parts[i], out _)) parts[i] = "{id}";
        }
        return "/" + string.Join('/', parts);
    }

    private static bool IsResourceMatch(string resource, string requestPath)
    {
        var r = NormalizePath(resource);
        return string.Equals(r, requestPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断当前用户是否属于"超级管理员"角色（Role.IsSuperAdmin = true 且 Role.Enabled = true）。
    /// 结果缓存 30 秒；角色变更会通过 PermissionCacheInvalidator.InvalidateUserRoles 清除。
    /// </summary>
    private bool IsSuperAdminUser(long userId)
    {
        if (userId <= 0) return false;
        var cacheKey = _cacheInvalidator.UserSuperAdminCacheKey(userId);
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            try
            {
                var roleIds = _userRoleRepo.Query()
                    .Where(x => x.UserId == userId)
                    .Select(x => x.RoleId)
                    .Distinct()
                    .ToList();
                if (roleIds.Count == 0) return false;
                return _roleRepo.Query().Any(r => roleIds.Contains(r.Id) && r.Enabled && r.IsSuperAdmin);
            }
            catch
            {
                return false;
            }
        });
    }
}
