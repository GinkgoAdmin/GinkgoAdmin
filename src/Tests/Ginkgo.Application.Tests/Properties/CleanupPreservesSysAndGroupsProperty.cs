// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 8：清理保留 sys 项与全部菜单组。
// 对应设计文档《Correctness Properties / Property 8》与任务 6.8，验证需求 7.3、7.4：
//   对于任意包含 Module='sys' 项与若干插件项的菜单组项集合，按某插件 Module 清理后：
//     1) 所有 Module='sys' 的主框架菜单组项保持不变（Id 集合不变、各项关键字段值不变）（需求 7.3）；
//     2) MenuGroup 记录集合完全不变（Id 集合不变、数量不变、各组关键字段值不变，不删除任何菜单组）（需求 7.4）。
//   作为完整性补充，断言被清理插件模块的项确实被全部移除（区分大小写）。
// 测试策略（与设计《Testing Strategy》一致）：使用 xUnit + FsCheck.Xunit（不自行实现属性测试框架），
//   单个属性测试、最少 100 次迭代；注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，
//   直接驱动 MenuGroupAppService.RemoveClientMenuItemsByModuleAsync。

using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// 清理保留 sys 项与全部菜单组属性测试（Property 8）。
/// </summary>
public sealed class CleanupPreservesSysAndGroupsProperty
{
    /// <summary>待清理目标可选的插件模块（均非 'sys'，区分大小写）。</summary>
    private static readonly string[] PluginModules =
    {
        "Ginkgo.Module.SmartCommunity",
        "Ginkgo.Module.Evaluate",
        "Ginkgo.Module.Demo"
    };

    /// <summary>
    /// 测试场景：
    /// - <see cref="Groups"/>：若干菜单组（含默认/非默认、单端/多端 ClientType、启用/禁用、系统/非系统），可为空集合；
    /// - <see cref="Items"/>：混合 Module（含 'sys' 与若干插件）的菜单组项集合，可为空集合；
    /// - <see cref="TargetModule"/>：本次清理的目标插件模块（非 'sys'）。
    /// </summary>
    private sealed record Scenario(
        List<MenuGroup> Groups,
        List<MenuGroupItem> Items,
        string TargetModule);

    /// <summary>菜单组关键字段快照，用于断言 MenuGroup 记录集合完全不变。</summary>
    private static (long Id, string Name, string Slug, string? ClientType, bool IsDefault,
        bool IsSystem, bool Enabled, string? Location, string? Version, int MaxDepth, string? Description)
        GroupSnapshot(MenuGroup g)
        => (g.Id, g.Name, g.Slug, g.ClientType, g.IsDefault, g.IsSystem, g.Enabled,
            g.Location, g.Version, g.MaxDepth, g.Description);

    /// <summary>菜单组项关键字段快照，用于断言 sys 项保持不变。</summary>
    private static (long Id, long? ParentId, long MenuGroupId, string Module, string? Url, string Title,
        string? Icon, bool RequireGrant, int Order, bool Enabled, string? Badge, string LinkType)
        ItemSnapshot(MenuGroupItem x)
        => (x.Id, x.ParentId, x.MenuGroupId, x.Module, x.Url, x.Title,
            x.Icon, x.RequireGrant, x.Order, x.Enabled, x.Badge, x.LinkType);

    // Feature: multi-client-plugin-portal, Property 8: 清理保留 sys 项与全部菜单组
    /// <summary>
    /// Property 8：对于任意包含 Module='sys' 项与若干插件项的数据集合，按某插件 Module 清理后，
    /// 所有 Module='sys' 的项保持不变，且 MenuGroup 记录集合完全不变（不删除任何菜单组）；
    /// 同时被清理插件模块的项被全部移除。
    /// **Validates: Requirements 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Cleanup_Should_Preserve_Sys_Items_And_All_Groups()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), scenario =>
        {
            var (groups, items, targetModule) = scenario;

            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);

            // 1. 播种菜单组与菜单组项。
            if (groups.Count > 0) groupRepo.AddRangeAsync(groups).GetAwaiter().GetResult();
            if (items.Count > 0) itemRepo.AddRangeAsync(items).GetAwaiter().GetResult();

            // 2. 清理前快照：全部菜单组、全部 'sys' 项。
            var groupsBefore = groupRepo.Query().ToList()
                .Select(GroupSnapshot).OrderBy(t => t.Id).ToList();
            var sysItemsBefore = itemRepo.Query().ToList()
                .Where(x => string.Equals(x.Module, "sys", StringComparison.Ordinal))
                .Select(ItemSnapshot).OrderBy(t => t.Id).ToList();

            // 3. 按目标插件模块清理。
            var service = BuildService(db);
            service.RemoveClientMenuItemsByModuleAsync(targetModule).GetAwaiter().GetResult();

            // 4. 清理后快照。
            var groupsAfter = groupRepo.Query().ToList()
                .Select(GroupSnapshot).OrderBy(t => t.Id).ToList();
            var sysItemsAfter = itemRepo.Query().ToList()
                .Where(x => string.Equals(x.Module, "sys", StringComparison.Ordinal))
                .Select(ItemSnapshot).OrderBy(t => t.Id).ToList();

            // 断言 1（需求 7.4）：MenuGroup 记录集合完全不变（数量与每条记录的关键字段均一致）。
            if (groupsBefore.Count != groupsAfter.Count) return false;
            for (var i = 0; i < groupsBefore.Count; i++)
            {
                if (!groupsBefore[i].Equals(groupsAfter[i])) return false;
            }

            // 断言 2（需求 7.3）：所有 'sys' 项保持不变（Id 集合与每项关键字段均一致）。
            if (sysItemsBefore.Count != sysItemsAfter.Count) return false;
            for (var i = 0; i < sysItemsBefore.Count; i++)
            {
                if (!sysItemsBefore[i].Equals(sysItemsAfter[i])) return false;
            }

            // 断言 3（完整性补充）：被清理插件模块的项已被全部移除（区分大小写）。
            var remainingTarget = itemRepo.Query().ToList()
                .Count(x => string.Equals(x.Module, targetModule, StringComparison.Ordinal));
            if (remainingTarget != 0) return false;

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 生成测试场景：先生成 0~5 个菜单组，再在其上生成 0~12 个混合模块（含 'sys'）的菜单组项，
    /// 最后随机选取一个插件模块作为清理目标（非 'sys'）。
    /// </summary>
    private static Gen<Scenario> ScenarioGen()
    {
        return GroupListGen().SelectMany(groups =>
        {
            var groupIds = groups.Select(g => g.Id).ToList();
            return ItemListGen(groupIds).SelectMany(items =>
                Gen.Elements(PluginModules).Select(target => new Scenario(groups, items, target)));
        });
    }

    /// <summary>
    /// 生成菜单组集合（含空集合）：0~5 个随机菜单组，覆盖默认/非默认、单端/多端、启用/禁用、系统/非系统。
    /// </summary>
    private static Gen<List<MenuGroup>> GroupListGen()
    {
        return Gen.Choose(0, 5).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<MenuGroup>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => PortalGenerators.MenuGroupGen()))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成菜单组项集合（含空集合）：0~12 个项，Module 混合（含 'sys' 与若干插件），
    /// 各项 MenuGroupId 随机落在已播种的菜单组上（无菜单组时落在随机 Id 上，不影响按 Module 清理语义）。
    /// </summary>
    private static Gen<List<MenuGroupItem>> ItemListGen(IReadOnlyList<long> groupIds)
    {
        return Gen.Choose(0, 12).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<MenuGroupItem>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => ItemGen(groupIds)))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单个菜单组项：随机 Module（含 'sys'）、标题/路径/图标/角标、授权语义、启用状态与排序号。
    /// </summary>
    private static Gen<MenuGroupItem> ItemGen(IReadOnlyList<long> groupIds)
    {
        return from module in PortalGenerators.Module()
               from title in PortalGenerators.Title()
               from url in PortalGenerators.Path()
               from icon in PortalGenerators.Icon()
               from badge in PortalGenerators.Badge()
               from requireGrant in PortalGenerators.Bool()
               from enabled in PortalGenerators.Bool()
               from order in PortalGenerators.OrderNo()
               from groupPick in Gen.Choose(0, Math.Max(0, groupIds.Count - 1))
               select BuildItem(
                   groupIds.Count == 0 ? SnowflakeIdGenerator.NextId() : groupIds[groupPick],
                   title, url, module, icon, badge, requireGrant, enabled, order);
    }

    private static MenuGroupItem BuildItem(long menuGroupId, string title, string url, string module,
        string? icon, string? badge, bool requireGrant, bool enabled, int order)
    {
        var item = MenuGroupItem.Create(
            menuGroupId: menuGroupId,
            title: title,
            linkType: "Custom",
            url: url,
            module: module,
            requireGrant: requireGrant);
        item.Icon = icon;
        item.Badge = badge;
        item.SetOrder(order);
        if (enabled) item.Enable(); else item.Disable();
        return item;
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
}
