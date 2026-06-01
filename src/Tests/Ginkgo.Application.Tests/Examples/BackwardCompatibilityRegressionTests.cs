// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）向后兼容回归示例测试（xUnit [Fact]）。
// 对应任务 18.4 与需求 13.1 / 13.2 / 13.3 / 13.4：证明本特性「新增能力」并未改变既有行为与语义。
//
// 设计依据（design.md《Requirement 13: 向后兼容》《Testing Strategy / 冒烟·示例测试》）：
//   13.1 不修改 ginkgo_Sys_Menu 表结构与既有数据语义（本特性无任何代码路径写 ginkgo_Sys_Menu）。
//   13.2 既有 Menus 段（ApplyMenusAsync/RemoveMenusAsync 写 ginkgo_Sys_Menu）行为不变；
//        新增的 ClientMenus 注入只写 ginkgo_Sys_MenuGroupItem，与 Menus→ginkgo_Sys_Menu 路径平行、互不干扰。
//   13.3 保留既有 ginkgo_Sys_RoleMenuGroup（组级授权）表与逻辑，并在其之上「新增」RoleMenuGroupItem（项级授权）；
//        二者各自独立、并存，互不替代。
//   13.4 不改变既有 GET /api/v1/menus/tree/my（后台 RBAC 菜单树，读 ginkgo_Sys_Menu）的行为与返回语义；
//        统一入口 GET /api/v1/client/portal（读 ginkgo_Sys_MenuGroupItem）是独立于它的新接口。
//
// 可验证性说明（务实取证策略）：
//   - 13.3 最直接可在进程内验证：本测试以 InMemoryTestDatabase + InMemoryRepository 驱动 MenuGroupAppService，
//     真实运行组级授权（SetRoleMenuGroupsAsync/GetRoleMenuGroupIdsAsync）与项级授权
//     （SetRoleMenuGroupItemsAsync/GetRoleMenuGroupItemIdsAsync），断言往返语义不变且两表彼此独立。
//   - 13.1 / 13.2 以「进程内代理不变量」取证：客户端入口注入（UpsertClientMenuItemsAsync）只写
//     ginkgo_Sys_MenuGroupItem，注入后 ginkgo_Sys_Menu（Menu 仓储）保持为空/不变；从而证明
//     ClientMenus 平行于 Menus、不触碰 Menus→ginkgo_Sys_Menu 路径。
//     注：唯一写 ginkgo_Sys_Menu 的生产代码为安装链路 ApplyMenusAsync/RemoveMenusAsync，本特性未修改之
//     （见 ModuleSqlExecutor.ApplyMenusAsync），故其行为不变属「按引用文档化」结论，本测试不重复其 DB/Web 宿主行为。
//   - 13.4 以「进程内代理不变量」取证：GetClientPortalAsync 构建入口树只读取 ginkgo_Sys_MenuGroupItem，
//     即便库中存在 ginkgo_Sys_Menu 行也不会泄漏进 portal 输出；从而证明 portal 路径与 menus/tree/my 路径相互区分。
//     注：menus/tree/my 由 MenusController 提供、读 ginkgo_Sys_Menu，本特性未修改该控制器，属「按引用文档化」结论。

using System.Linq;
using System.Threading.Tasks;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Utils;
using Xunit;
using AppSpec = Ginkgo.Application.Menus.ClientMenuItemSpec;

namespace Ginkgo.Application.Tests.Examples;

/// <summary>
/// 向后兼容回归示例测试：证明本特性新增的「默认菜单组 / 客户端入口 / 项级授权 / portal 接口」
/// 不改变既有 Menus / menus/tree/my / ginkgo_Sys_Menu / ginkgo_Sys_RoleMenuGroup 的行为与语义。
/// </summary>
public sealed class BackwardCompatibilityRegressionTests
{
    /// <summary>
    /// 以内存仓储装配 <see cref="MenuGroupAppService"/>（构造需 8 个仓储依赖，均基于同一内存库）。
    /// </summary>
    private static MenuGroupAppService BuildService(InMemoryTestDatabase db)
    {
        return new MenuGroupAppService(
            new InMemoryRepository<MenuGroup>(db),
            new InMemoryRepository<MenuGroupItem>(db),
            new InMemoryRepository<RoleMenuGroup>(db),
            new InMemoryRepository<Menu>(db),
            new InMemoryRepository<UserRole>(db),
            new InMemoryRepository<Role>(db),
            new InMemoryRepository<RolePermission>(db),
            new InMemoryRepository<RoleMenuGroupItem>(db));
    }

    /// <summary>
    /// 预置某单端类型的默认菜单组（IsDefault=1、IsSystem=1、Enabled=1），作为客户端入口注入/查询目标。
    /// </summary>
    private static MenuGroup SeedDefaultGroup(InMemoryTestDatabase db, string clientType)
    {
        var groupRepo = new InMemoryRepository<MenuGroup>(db);
        var seed = SnowflakeIdGenerator.NextId();
        var group = MenuGroup.Create(
            name: "默认组-" + seed,
            slug: "default-" + seed,
            clientType: clientType,
            isSystem: true,
            isDefault: true);
        group.Enable();
        groupRepo.AddAsync(group).GetAwaiter().GetResult();
        return group;
    }

    // ===== 13.3：既有组级授权（ginkgo_Sys_RoleMenuGroup）往返语义保持不变 =====

    /// <summary>
    /// 既有「角色—菜单组」组级授权 API（SetRoleMenuGroupsAsync / GetRoleMenuGroupIdsAsync）行为保持不变：
    /// 设置一组菜单组授权后查询应原样返回（去重、集合相等），二次覆盖应为全量覆盖语义。
    /// 这是本特性「保留既有 ginkgo_Sys_RoleMenuGroup 表与逻辑」的直接回归证据（需求 13.3）。
    /// </summary>
    [Fact]
    public async Task RoleMenuGroup_GroupLevel_RoundTrip_StillWorks()
    {
        using var db = new InMemoryTestDatabase();
        var service = BuildService(db);

        const long roleId = 1001L;

        // 设置组级授权（组 Id 含重复，验证去重）
        await service.SetRoleMenuGroupsAsync(new SetRoleMenuGroupsInput
        {
            RoleId = roleId,
            MenuGroupIds = new() { 11L, 22L, 33L, 22L }
        });

        var got = await service.GetRoleMenuGroupIdsAsync(roleId);

        // 往返：查询结果等于提交集合去重后的集合（既有组级授权语义不变）
        Assert.Equal(new[] { 11L, 22L, 33L }.OrderBy(x => x), got.OrderBy(x => x));

        // 二次设置应为「全量覆盖」而非合并（既有语义）
        await service.SetRoleMenuGroupsAsync(new SetRoleMenuGroupsInput
        {
            RoleId = roleId,
            MenuGroupIds = new() { 44L }
        });

        var afterOverride = await service.GetRoleMenuGroupIdsAsync(roleId);
        Assert.Equal(new[] { 44L }, afterOverride.ToArray());
    }

    // ===== 13.3：组级授权与项级授权两表/两逻辑彼此独立、并存 =====

    /// <summary>
    /// 组级授权（RoleMenuGroup）与项级授权（RoleMenuGroupItem）相互独立、并存：
    ///   - 设置组级授权不会写出任何 RoleMenuGroupItem 行；
    ///   - 设置项级授权不会写出任何 RoleMenuGroup 行；
    ///   - 同一角色可同时持有两类授权，互不影响。
    /// 证明本特性是「在既有组级授权之上新增项级授权」，而非替换既有表/逻辑（需求 13.3）。
    /// </summary>
    [Fact]
    public async Task GroupLevel_And_ItemLevel_Grants_Are_Independent()
    {
        using var db = new InMemoryTestDatabase();
        var roleMenuGroupRepo = new InMemoryRepository<RoleMenuGroup>(db);
        var roleMenuGroupItemRepo = new InMemoryRepository<RoleMenuGroupItem>(db);
        var service = BuildService(db);

        const long roleId = 2002L;

        // 1. 仅设置组级授权：应只产生 RoleMenuGroup 行，RoleMenuGroupItem 表保持为空
        await service.SetRoleMenuGroupsAsync(new SetRoleMenuGroupsInput
        {
            RoleId = roleId,
            MenuGroupIds = new() { 100L, 200L }
        });

        Assert.Equal(2, roleMenuGroupRepo.Query().Where(x => x.RoleId == roleId).ToList().Count);
        Assert.Empty(roleMenuGroupItemRepo.Query().ToList());

        // 2. 再设置项级授权：应只新增 RoleMenuGroupItem 行，既有 RoleMenuGroup 行不受影响
        await service.SetRoleMenuGroupItemsAsync(new SetRoleMenuGroupItemsInput
        {
            RoleId = roleId,
            MenuGroupItemIds = new() { 300L, 400L, 500L }
        });

        // 组级授权仍是 2 条（未被项级授权操作改动）
        Assert.Equal(2, roleMenuGroupRepo.Query().Where(x => x.RoleId == roleId).ToList().Count);
        // 项级授权为 3 条
        Assert.Equal(3, roleMenuGroupItemRepo.Query().Where(x => x.RoleId == roleId).ToList().Count);

        // 3. 两类授权的查询互不干扰，各自返回各自的集合
        var groupIds = await service.GetRoleMenuGroupIdsAsync(roleId);
        var itemIds = await service.GetRoleMenuGroupItemIdsAsync(roleId);

        Assert.Equal(new[] { 100L, 200L }.OrderBy(x => x), groupIds.OrderBy(x => x));
        Assert.Equal(new[] { 300L, 400L, 500L }.OrderBy(x => x), itemIds.OrderBy(x => x));
    }

    // ===== 13.1 / 13.2：客户端入口注入只写 MenuGroupItem，不触碰 ginkgo_Sys_Menu =====

    /// <summary>
    /// 客户端入口注入（UpsertClientMenuItemsAsync）只写 ginkgo_Sys_MenuGroupItem，绝不写 ginkgo_Sys_Menu：
    ///   - 注入后 MenuGroupItem 含被注入的入口项（Module=插件Id）；
    ///   - 注入前后 Menu（ginkgo_Sys_Menu）仓储始终为空（该调用不创建任何 Menu 行）。
    /// 证明 ClientMenus 路径平行于既有 Menus 路径，不改变 Menus→ginkgo_Sys_Menu 的写入行为（需求 13.1 / 13.2）。
    /// 备注：写 ginkgo_Sys_Menu 的唯一生产代码是安装链路 ApplyMenusAsync/RemoveMenusAsync，本特性未修改之，
    ///       其行为不变属「按引用文档化」结论（见 ModuleSqlExecutor.ApplyMenusAsync），此处不重复其 DB 行为。
    /// </summary>
    [Fact]
    public async Task ClientMenus_Injection_Writes_MenuGroupItem_Not_SysMenu()
    {
        using var db = new InMemoryTestDatabase();
        var menuRepo = new InMemoryRepository<Menu>(db);
        var itemRepo = new InMemoryRepository<MenuGroupItem>(db);
        var service = BuildService(db);

        const string moduleId = "Ginkgo.Module.SmartCommunity";
        var group = SeedDefaultGroup(db, PortalClientTypes.Uniapp);

        // 注入前：ginkgo_Sys_Menu 为空
        Assert.Empty(menuRepo.Query().ToList());

        // 注入两个客户端入口项（写入 UNIAPP 默认组）
        await service.UpsertClientMenuItemsAsync(PortalClientTypes.Uniapp, moduleId, new[]
        {
            new AppSpec { Title = "事件办理", Icon = "ri-mic-line", Path = "/pages/plugins/smart-community/event-handle", RequireGrant = true, Order = 1 },
            new AppSpec { Title = "智慧社区", Icon = "ri-community-line", Path = "/pages/plugins/smart-community/index", RequireGrant = false, Order = 2 }
        });

        // 注入后：MenuGroupItem 含该模块的 2 个入口项，且均落在 UNIAPP 默认组下
        var injected = await service.GetItemsByModuleAsync(moduleId);
        Assert.Equal(2, injected.Count);
        Assert.All(injected, x => Assert.Equal(group.Id, x.MenuGroupId));
        Assert.All(injected, x => Assert.Equal(moduleId, x.Module));

        // 关键回归断言：注入只写 MenuGroupItem，ginkgo_Sys_Menu 始终为空（未被该调用创建任何行）
        Assert.Empty(menuRepo.Query().ToList());

        // 进一步佐证：MenuGroupItem 与 Menu 是两张完全不同的表，注入项数 = 2，Menu 行数 = 0
        Assert.Equal(2, itemRepo.Query().ToList().Count);
    }

    // ===== 13.4：portal 入口树只读 MenuGroupItem，不会泄漏 ginkgo_Sys_Menu 数据 =====

    /// <summary>
    /// 统一入口 GetClientPortalAsync 构建入口树只读取 ginkgo_Sys_MenuGroupItem，
    /// 即便库中存在 ginkgo_Sys_Menu 行也绝不泄漏进 portal 输出：
    ///   - 预置若干 ginkgo_Sys_Menu 行（模拟既有后台 RBAC 菜单数据，供 menus/tree/my 使用）；
    ///   - 向 UNIAPP 默认组注入若干公共可见入口项（RequireGrant=false）；
    ///   - 调用 portal 后，返回项全部来自注入的 MenuGroupItem，数量与之相符，
    ///     且不含任何与 ginkgo_Sys_Menu 行同名/同地址的项；Menu 行在调用后保持不变。
    /// 证明 portal 路径（读 MenuGroupItem）与 menus/tree/my 路径（读 ginkgo_Sys_Menu）相互区分（需求 13.4）。
    /// 备注：menus/tree/my 由 MenusController 提供、读 ginkgo_Sys_Menu，本特性未修改该控制器，
    ///       其行为与返回语义不变属「按引用文档化」结论，本测试以「portal 不消费 Menu 仓储」作进程内代理验证。
    /// </summary>
    [Fact]
    public async Task Portal_Reads_MenuGroupItem_Not_SysMenu()
    {
        using var db = new InMemoryTestDatabase();
        var menuRepo = new InMemoryRepository<Menu>(db);
        var service = BuildService(db);

        const string moduleId = "Ginkgo.Module.SmartCommunity";
        SeedDefaultGroup(db, PortalClientTypes.Uniapp);

        // 预置 ginkgo_Sys_Menu 行（既有后台 RBAC 菜单，仅供 menus/tree/my，不应进入 portal 输出）
        await menuRepo.AddAsync(Menu.Create("系统管理", "Menu", "/admin/system", null, "ri-settings-line", null, "admin:system"));
        await menuRepo.AddAsync(Menu.Create("用户管理", "Menu", "/admin/users", null, "ri-user-line", null, "admin:users"));
        var sysMenuCountBefore = menuRepo.Query().ToList().Count;
        Assert.Equal(2, sysMenuCountBefore);

        // 注入公共可见入口项（RequireGrant=false，无角色用户也可见）到 UNIAPP 默认组
        await service.UpsertClientMenuItemsAsync(PortalClientTypes.Uniapp, moduleId, new[]
        {
            new AppSpec { Title = "智慧社区", Icon = "ri-community-line", Path = "/pages/plugins/smart-community/index", RequireGrant = false, Order = 1 },
            new AppSpec { Title = "我的报修", Icon = "ri-tools-line", Path = "/pages/plugins/smart-community/repair", RequireGrant = false, Order = 2 }
        });

        // 以无角色用户（userId=null）查询 MOBILE 端入口树：应只返回公共可见的 MenuGroupItem 入口
        var portal = await service.GetClientPortalAsync("MOBILE", userId: null);

        Assert.Equal(PortalClientTypes.Uniapp, portal.ClientType);
        Assert.Equal(2, portal.Items.Count);

        // 返回项的地址全部来自注入的 MenuGroupItem（path），而非 ginkgo_Sys_Menu 的 Route
        var portalUrls = portal.Items.Select(x => x.Url).OrderBy(x => x).ToArray();
        Assert.Equal(
            new[] { "/pages/plugins/smart-community/index", "/pages/plugins/smart-community/repair" }.OrderBy(x => x),
            portalUrls);

        // ginkgo_Sys_Menu 的菜单地址/标题不会泄漏进 portal 输出
        Assert.DoesNotContain(portal.Items, x => x.Url == "/admin/system" || x.Url == "/admin/users");
        Assert.DoesNotContain(portal.Items, x => x.Title == "系统管理" || x.Title == "用户管理");

        // portal 查询不消费/不改动 ginkgo_Sys_Menu：调用后 Menu 行数保持不变
        Assert.Equal(sysMenuCountBefore, menuRepo.Query().ToList().Count);
    }
}
