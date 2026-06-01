// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）任务 17.1 的集成测试。
// Integration: multi-client-plugin-portal — [LoginOnly] 鉴权（Requirement 9.2）。
//
// 目标：断言「已登录的普通（非超管）用户可访问 /client/portal，且该访问不触发
// ginkgo_Sys_Menu 资源权限映射」。即验证 ClientPortalController.GetPortalAsync 上标注的
// [LoginOnly] 能让真实的 PermissionAuthorizationHandler 走「仅登录放行」分支，
// 而不去查询 ginkgo_Sys_Menu（Menu 表）做 Resource+Method 资源映射授权。
//
// 测试约束与说明：
// - 本仓库当前没有 WebApplicationFactory / TestServer 形式的 API 集成测试基座，
//   因此采用「针对真实鉴权处理器的集成式测试」：直接构造真实的
//   Ginkgo.Api.Auth.PermissionAuthorizationHandler（复用既有鉴权链路，不新建任何鉴权机制），
//   注入由 SQLite 内存库支撑的真实 IRepository<T>，并构造携带 [LoginOnly] 端点元数据、
//   路径为 /api/v1/client/portal 的 HttpContext 作为鉴权资源，调用 HandleAsync 后断言放行。
// - 为精确断言「不触发 ginkgo_Sys_Menu 资源映射」，对 Menu 仓储使用记账代理（spy），
//   记录其 Query() 被调用次数；[LoginOnly] 命中时该次数必须为 0。
// - 通过「无 [LoginOnly] 的对照用例」证明：同一普通用户在没有 [LoginOnly] 时会因
//   需要菜单资源授权且未被授权而被拒，从而隔离出 [LoginOnly] 正是放行的唯一原因。

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Api.Auth;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Ginkgo.Plugin.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using SqlSugar;
using Xunit;

namespace Ginkgo.Application.Tests.Integration;

/// <summary>
/// [LoginOnly] 鉴权集成测试：验证已登录普通用户可访问 /client/portal 且不触发菜单资源映射。
/// </summary>
public sealed class LoginOnlyAuthorizationIntegrationTests
{
    // 统一入口接口的请求路径与方法（与 ClientPortalController 路由一致）。
    private const string PortalPath = "/api/v1/client/portal";
    private const string PortalMethod = "GET";

    // 测试用的普通用户与普通角色 Id（非超管、无任何菜单授权）。
    private const long OrdinaryUserId = 20001L;
    private const long OrdinaryRoleId = 30001L;

    /// <summary>
    /// 已登录普通用户访问标注了 [LoginOnly] 的 /client/portal：应放行，且全程不查询 ginkgo_Sys_Menu。
    /// </summary>
    [Fact]
    public async Task LoggedInOrdinaryUser_CanAccessPortal_WithLoginOnly_AndDoesNotConsultSysMenu()
    {
        using var db = new InMemoryTestDatabase();

        // 即使库中存在一条能匹配 /client/portal 的菜单资源映射，且普通用户未被授权该资源，
        // [LoginOnly] 也应让其在查询菜单前直接放行——以此凸显「跳过 ginkgo_Sys_Menu 资源映射」。
        SeedMatchingPortalMenuResource(db);
        SeedOrdinaryUserWithRole(db);

        var menuSpy = new RecordingMenuRepository(new InMemoryRepository<Menu>(db));
        var handler = CreateHandler(db, menuSpy);

        var httpContext = BuildHttpContext(OrdinaryUserId, withLoginOnly: true);
        var requirement = new PermissionRequirement();
        var context = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { requirement },
            httpContext.User,
            httpContext);

        await handler.HandleAsync(context);

        // 断言 1：普通已登录用户被放行（[LoginOnly] 仅要求登录即可）。
        Assert.True(context.HasSucceeded, "已登录普通用户访问 [LoginOnly] 的 /client/portal 应被放行");

        // 断言 2：未触发 ginkgo_Sys_Menu 资源映射（Menu 表仓储 Query() 未被调用）。
        Assert.Equal(0, menuSpy.QueryCallCount);
    }

    /// <summary>
    /// 对照用例：去掉 [LoginOnly] 后，同一普通用户因需要菜单资源授权且未被授权而被拒，
    /// 且该过程会真实查询 ginkgo_Sys_Menu。用以隔离证明 [LoginOnly] 是放行的唯一原因。
    /// </summary>
    [Fact]
    public async Task SameOrdinaryUser_WithoutLoginOnly_IsDenied_AndConsultsSysMenu()
    {
        using var db = new InMemoryTestDatabase();

        SeedMatchingPortalMenuResource(db);
        SeedOrdinaryUserWithRole(db);

        var menuSpy = new RecordingMenuRepository(new InMemoryRepository<Menu>(db));
        var handler = CreateHandler(db, menuSpy);

        var httpContext = BuildHttpContext(OrdinaryUserId, withLoginOnly: false);
        var requirement = new PermissionRequirement();
        var context = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { requirement },
            httpContext.User,
            httpContext);

        await handler.HandleAsync(context);

        // 无 [LoginOnly] 时，普通用户未被授权该资源 → 不放行。
        Assert.False(context.HasSucceeded, "无 [LoginOnly] 时未授权的普通用户不应被放行");

        // 且该路径会真实查询 ginkgo_Sys_Menu 做资源映射（与上一用例形成对照）。
        Assert.True(menuSpy.QueryCallCount >= 1, "无 [LoginOnly] 时应查询 ginkgo_Sys_Menu 资源映射");
    }

    /// <summary>
    /// 构造真实的权限鉴权处理器，注入由 SQLite 内存库支撑的真实仓储（菜单仓储替换为记账代理）。
    /// </summary>
    private static PermissionAuthorizationHandler CreateHandler(InMemoryTestDatabase db, IRepository<Menu> menuRepo)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var invalidator = new PermissionCacheInvalidator(cache);
        return new PermissionAuthorizationHandler(
            new InMemoryRepository<RolePermission>(db),
            new InMemoryRepository<UserRole>(db),
            menuRepo,
            new InMemoryRepository<Role>(db),
            cache,
            invalidator);
    }

    /// <summary>
    /// 写入一条能匹配 GET /api/v1/client/portal 的启用 Api 资源菜单（普通用户并未被授权该菜单）。
    /// </summary>
    private static void SeedMatchingPortalMenuResource(InMemoryTestDatabase db)
    {
        var menu = Menu.Create(
            name: "客户端入口接口",
            type: "Api",
            route: null,
            parentId: null,
            icon: null,
            url: null,
            code: "client:portal");
        menu.Resource = PortalPath;
        menu.Method = PortalMethod;
        menu.Enable();
        db.Client.Insertable(menu).ExecuteCommand();
    }

    /// <summary>
    /// 写入一个普通（非超管）用户及其普通角色关联：用户已登录但没有任何菜单资源授权。
    /// </summary>
    private static void SeedOrdinaryUserWithRole(InMemoryTestDatabase db)
    {
        var role = new Role
        {
            Id = OrdinaryRoleId,
            Name = "普通用户",
            Code = "ORDINARY",
            Enabled = true,
            IsSuperAdmin = false
        };
        db.Client.Insertable(role).ExecuteCommand();

        var userRole = new UserRole
        {
            UserId = OrdinaryUserId,
            RoleId = OrdinaryRoleId
        };
        db.Client.Insertable(userRole).ExecuteCommand();
    }

    /// <summary>
    /// 构造携带登录态与端点元数据的 HttpContext：路径为 /client/portal，按需附加 [LoginOnly] 元数据。
    /// </summary>
    private static HttpContext BuildHttpContext(long userId, bool withLoginOnly)
    {
        var httpContext = new DefaultHttpContext();

        // 登录态：通过 NameIdentifier Claim 提供用户 Id（与 PermissionAuthorizationHandler 解析方式一致）。
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            authenticationType: "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        httpContext.Request.Method = PortalMethod;
        httpContext.Request.Path = PortalPath;

        // 端点元数据：模拟 ClientPortalController.GetPortalAsync 上标注的 [LoginOnly]。
        var metadata = withLoginOnly
            ? new EndpointMetadataCollection(new LoginOnlyAttribute())
            : EndpointMetadataCollection.Empty;
        var endpoint = new Endpoint(
            requestDelegate: null,
            metadata: metadata,
            displayName: "ClientPortalController.GetPortalAsync");
        httpContext.SetEndpoint(endpoint);

        return httpContext;
    }

    /// <summary>
    /// Menu 仓储记账代理：透传内部真实仓储的全部操作，并记录 Query() 调用次数，
    /// 用于断言「是否触发了 ginkgo_Sys_Menu 资源映射」。
    /// </summary>
    private sealed class RecordingMenuRepository : IRepository<Menu>
    {
        private readonly IRepository<Menu> _inner;

        public RecordingMenuRepository(IRepository<Menu> inner) => _inner = inner;

        /// <summary>
        /// 记录 Query() 被调用的次数（即菜单资源映射被查询的次数）。
        /// </summary>
        public int QueryCallCount { get; private set; }

        public ISugarQueryable<Menu> Query()
        {
            QueryCallCount++;
            return _inner.Query();
        }

        public Task<Menu?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task AddAsync(Menu entity, CancellationToken cancellationToken = default)
            => _inner.AddAsync(entity, cancellationToken);

        public Task UpdateAsync(Menu entity, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(entity, cancellationToken);

        public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(id, cancellationToken);

        public Task AddRangeAsync(IEnumerable<Menu> entities, CancellationToken cancellationToken = default)
            => _inner.AddRangeAsync(entities, cancellationToken);

        public Task UpdateRangeAsync(IEnumerable<Menu> entities, CancellationToken cancellationToken = default)
            => _inner.UpdateRangeAsync(entities, cancellationToken);

        public Task DeleteRangeAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
            => _inner.DeleteRangeAsync(ids, cancellationToken);

        public Task<long> CountAsync(CancellationToken cancellationToken = default)
            => _inner.CountAsync(cancellationToken);

        public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
            => _inner.ExistsAsync(id, cancellationToken);

        public Task<IEnumerable<Menu>> GetAllAsync(CancellationToken cancellationToken = default)
            => _inner.GetAllAsync(cancellationToken);
    }
}
