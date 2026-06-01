// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）集成测试 —— 租户隔离。
// Integration: multi-client-plugin-portal — 租户隔离（Requirements 6.6, 9.11）
//
// 验证目标（对应设计文档《Testing Strategy / 集成测试》、任务 17.2）：
//   不同租户上下文下，经统一入口注入链路写入的入口数据（MenuGroupItem）互不可见；
//   即：在租户 A 上下文注入的入口项，在租户 B 上下文下的「按模块查询」与「统一入口树查询」均不可见，反之亦然。
//
// 关于「租户上下文」的工程化建模与选型说明（已核实框架现状后的结论）：
//   1) 经检索主框架 src/Server 全量代码，MenuGroup / MenuGroupItem / RoleMenuGroupItem 等
//      菜单组相关实体「不含 TenantId 行级列」，框架对这些实体也「没有行级租户过滤」。
//      真实仓储 SqlSugarRepository<T>.Query() 仅做软删除过滤与可选的「数据范围（部门/本人）」过滤，
//      并不存在可设置的「环境内当前租户键」来驱动这些实体的行级隔离。
//   2) 因此本特性入口数据（MenuGroupItem）的「租户隔离」在框架层面等价于「按库/按连接隔离」：
//      入口数据读写全部经 IRepository<T>.Query() 落到对应的 ISqlSugarClient 连接，
//      不同租户的隔离由其各自连接/数据库承载（需求 6.6/9.11 所述「遵循既有租户隔离链路」即指此链路）。
//   3) 复用既有测试基础设施：InMemoryRepository 注释已明确「每个 InMemoryTestDatabase 实例对应一份
//      相互隔离的内存库，不同上下文之间数据天然不可见，等价于框架既有的多租户（按库隔离）链路」。
//      故本集成测试以「两个独立 InMemoryTestDatabase 实例」分别代表「租户 A 上下文」与「租户 B 上下文」，
//      在各自上下文装配独立的 MenuGroupAppService，真实驱动注入与查询逻辑（不 mock SqlSugar、不伪造数据），
//      从而忠实复刻框架按库隔离的租户链路，断言跨租户数据互不可见。

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Utils;
using Xunit;
// 消歧义：注入方法 UpsertClientMenuItemsAsync 接受「应用层」规格类型，
// 与测试基础设施侧的同名类型（Infrastructure.ClientMenuItemSpec）区分开。
using ClientMenuItemSpec = Ginkgo.Application.Menus.ClientMenuItemSpec;

namespace Ginkgo.Application.Tests.Integration;

/// <summary>
/// 租户隔离集成测试：验证不同租户上下文下注入/查询的入口数据互不可见（需求 6.6、9.11）。
/// </summary>
public sealed class TenantIsolationIntegrationTests
{
    /// <summary>注入入口项所用的插件模块标识（两个租户刻意使用同一模块，凸显隔离来自上下文而非模块差异）。</summary>
    private const string Module = "Ginkgo.Module.Demo";

    /// <summary>统一入口接口对外的移动端入参（归一化后映射为 UNIAPP）。</summary>
    private const string MobileClientType = "MOBILE";

    /// <summary>查询入口树所用的固定用户 Id（无角色普通登录用户；RequireGrant=0 的公共入口对其可见）。</summary>
    private const long QueryUserId = 9_001L;

    /// <summary>
    /// 单个租户上下文：持有独立内存库、独立装配的应用服务，以及该租户下默认 UNIAPP 菜单组 Id。
    /// </summary>
    private sealed class TenantContext : System.IDisposable
    {
        public InMemoryTestDatabase Db { get; }
        public MenuGroupAppService Service { get; }
        public long DefaultGroupId { get; }

        public TenantContext(string slugSeed)
        {
            Db = new InMemoryTestDatabase();
            Service = BuildService(Db);

            // 在该租户上下文内播种一个 UNIAPP 端默认菜单组（IsDefault=1），作为入口注入的锚点。
            var groupRepo = new InMemoryRepository<MenuGroup>(Db);
            var group = MenuGroup.Create(
                name: "默认移动端",
                slug: "default-uniapp-" + slugSeed,
                clientType: PortalClientTypes.Uniapp,
                isSystem: true,
                isDefault: true);
            groupRepo.AddAsync(group).GetAwaiter().GetResult();
            DefaultGroupId = group.Id;
        }

        public void Dispose() => Db.Dispose();
    }

    /// <summary>
    /// 以内存仓储装配 <see cref="MenuGroupAppService"/>（构造函数所需 8 个仓储均基于同一内存库）。
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
    /// 构造一组客户端入口声明（均为公共可见入口，RequireGrant=false，保证普通登录用户可在入口树中看到）。
    /// </summary>
    private static List<ClientMenuItemSpec> Specs(params (string Title, string Path)[] items)
    {
        return items.Select(x => new ClientMenuItemSpec
        {
            Title = x.Title,
            Icon = "ri-home-line",
            Path = x.Path,
            RequireGrant = false,
            Order = 0,
            Badge = null
        }).ToList();
    }

    /// <summary>
    /// 在租户 A 上下文注入入口后，租户 B 上下文「按模块查询」与「统一入口树查询」均看不到租户 A 的入口数据，反之亦然。
    /// **Validates: Requirements 6.6, 9.11**
    /// </summary>
    [Fact]
    public async Task Injected_Portal_Items_Should_Be_Invisible_Across_Tenant_Contexts()
    {
        // 两个独立上下文分别代表租户 A 与租户 B（按库隔离，等价于框架既有多租户链路）。
        using var tenantA = new TenantContext("a");
        using var tenantB = new TenantContext("b");

        // 租户 A：向其 UNIAPP 默认组注入两个入口项。
        var pathA1 = "/pages/plugins/demo/a-event";
        var pathA2 = "/pages/plugins/demo/a-center";
        await tenantA.Service.UpsertClientMenuItemsAsync(
            MobileClientType, Module, Specs(("甲租户事件", pathA1), ("甲租户中心", pathA2)));

        // 租户 B：向其 UNIAPP 默认组注入一个入口项（刻意使用同一模块名，凸显隔离来自上下文）。
        var pathB1 = "/pages/plugins/demo/b-report";
        await tenantB.Service.UpsertClientMenuItemsAsync(
            MobileClientType, Module, Specs(("乙租户上报", pathB1)));

        // ===== 断言 1：按模块查询互不可见（需求 6.6/9.11 写入与读取均遵循按库隔离链路）=====
        var aItemsByModule = await tenantA.Service.GetItemsByModuleAsync(Module);
        var bItemsByModule = await tenantB.Service.GetItemsByModuleAsync(Module);

        var aPaths = aItemsByModule.Select(x => x.Url).ToHashSet();
        var bPaths = bItemsByModule.Select(x => x.Url).ToHashSet();

        // 租户 A 仅能看到自己注入的两项，且不含租户 B 的任何项。
        Assert.Equal(2, aItemsByModule.Count);
        Assert.Contains(pathA1, aPaths);
        Assert.Contains(pathA2, aPaths);
        Assert.DoesNotContain(pathB1, aPaths);

        // 租户 B 仅能看到自己注入的一项，且不含租户 A 的任何项。
        Assert.Single(bItemsByModule);
        Assert.Contains(pathB1, bPaths);
        Assert.DoesNotContain(pathA1, bPaths);
        Assert.DoesNotContain(pathA2, bPaths);

        // ===== 断言 2：统一入口树查询互不可见（GET /client/portal 等价应用层调用）=====
        var aPortal = await tenantA.Service.GetClientPortalAsync(MobileClientType, QueryUserId);
        var bPortal = await tenantB.Service.GetClientPortalAsync(MobileClientType, QueryUserId);

        var aPortalPaths = Flatten(aPortal.Items).Select(x => x.Url).ToHashSet();
        var bPortalPaths = Flatten(bPortal.Items).Select(x => x.Url).ToHashSet();

        // 租户 A 入口树只含自己的项，绝不含租户 B 的项。
        Assert.Equal(tenantA.DefaultGroupId, aPortal.GroupId);
        Assert.Equal(2, aPortalPaths.Count);
        Assert.Contains(pathA1, aPortalPaths);
        Assert.Contains(pathA2, aPortalPaths);
        Assert.DoesNotContain(pathB1, aPortalPaths);

        // 租户 B 入口树只含自己的项，绝不含租户 A 的项。
        Assert.Equal(tenantB.DefaultGroupId, bPortal.GroupId);
        Assert.Single(bPortalPaths);
        Assert.Contains(pathB1, bPortalPaths);
        Assert.DoesNotContain(pathA1, bPortalPaths);
        Assert.DoesNotContain(pathA2, bPortalPaths);
    }

    /// <summary>
    /// 在租户 A 注入后，租户 B 即使尚未注入任何入口，其「按模块查询」与「入口树」也应为空，
    /// 证明跨租户写入不会泄漏到另一个租户上下文（需求 6.6/9.11）。
    /// **Validates: Requirements 6.6, 9.11**
    /// </summary>
    [Fact]
    public async Task Injection_In_One_Tenant_Should_Not_Leak_To_An_Empty_Tenant()
    {
        using var tenantA = new TenantContext("a");
        using var tenantB = new TenantContext("b");

        // 仅租户 A 注入入口；租户 B 不做任何注入。
        await tenantA.Service.UpsertClientMenuItemsAsync(
            MobileClientType, Module, Specs(("甲租户唯一入口", "/pages/plugins/demo/only-a")));

        // 租户 A 能查询到自己的注入项。
        var aItems = await tenantA.Service.GetItemsByModuleAsync(Module);
        Assert.Single(aItems);

        // 租户 B 上下文下：按模块查询为空、入口树为空（默认组存在但无任何项）。
        var bItems = await tenantB.Service.GetItemsByModuleAsync(Module);
        Assert.Empty(bItems);

        var bPortal = await tenantB.Service.GetClientPortalAsync(MobileClientType, QueryUserId);
        Assert.Equal(tenantB.DefaultGroupId, bPortal.GroupId);
        Assert.Empty(Flatten(bPortal.Items));
    }

    /// <summary>
    /// 递归展开入口树为扁平节点列表，便于按 Url 断言可见性。
    /// </summary>
    private static IEnumerable<ClientPortalItemDto> Flatten(List<ClientPortalItemDto>? nodes)
    {
        if (nodes == null) yield break;
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
