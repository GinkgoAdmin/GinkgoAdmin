// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 4：按模块过滤返回该模块全部且仅该模块的项。
// 对应设计文档《Correctness Properties / Property 4》与任务 6.4，验证需求 2.4：
//   对于任意由多个模块（含 'sys'）混合构成的菜单组项集合，按某一 Module 值过滤的结果集合，
//   恰好等于该集合中 Module 等于该值的项集合（区分大小写）。
// 测试策略（与设计《Testing Strategy》一致）：使用 xUnit + FsCheck.Xunit（不自行实现属性测试框架），
//   单个属性测试、最少 100 次迭代；注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，
//   直接驱动 MenuGroupAppService.GetItemsByModuleAsync。

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

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// 按模块过滤属性测试（Property 4）。
/// </summary>
public sealed class ModuleFilterProperty
{
    /// <summary>
    /// 生成单个菜单组项：随机 Module（含 'sys' 与若干插件 ModuleId）、随机标题/路径/排序/授权语义。
    /// 每个项归属一个随机菜单组（1~5），Id 由工厂方法分配的雪花 Id 保证唯一。
    /// </summary>
    private static Gen<MenuGroupItem> ItemGen()
    {
        return from module in PortalGenerators.Module()
               from title in PortalGenerators.Title()
               from url in PortalGenerators.Path()
               from groupSeed in Gen.Choose(1, 5)
               from requireGrant in PortalGenerators.Bool()
               from order in PortalGenerators.OrderNo()
               select BuildItem(groupSeed, title, url, module, requireGrant, order);
    }

    /// <summary>
    /// 生成测试场景：0~12 个混合模块的菜单组项集合（含空集合），以及一个待过滤的目标 Module 值。
    /// 目标 Module 独立从同一模块池中选取，使过滤目标既可能命中多项、也可能零命中（覆盖空结果边界）。
    /// </summary>
    private static Gen<(List<MenuGroupItem> Items, string TargetModule)> ScenarioGen()
    {
        return Gen.Choose(0, 12).SelectMany(count =>
        {
            var itemsGen = count == 0
                ? Gen.Constant(new List<MenuGroupItem>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => ItemGen()))
                    .Select(seq => seq.ToList());
            return itemsGen.SelectMany(items =>
                PortalGenerators.Module().Select(target => (items, target)));
        });
    }

    private static MenuGroupItem BuildItem(long menuGroupId, string title, string url,
        string module, bool requireGrant, int order)
    {
        var item = MenuGroupItem.Create(
            menuGroupId: menuGroupId,
            title: title,
            linkType: "Custom",
            url: url,
            module: module,
            requireGrant: requireGrant);
        item.SetOrder(order);
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

    // Feature: multi-client-plugin-portal, Property 4: 按模块过滤返回该模块全部且仅该模块的项
    /// <summary>
    /// Property 4：对于任意由多个模块（含 'sys'）混合构成的菜单组项集合，按某一 Module 值过滤的结果集合，
    /// 恰好等于该集合中 Module 等于该值（区分大小写）的项集合；且返回的每个项 Module 均等于过滤值。
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void FilterByModule_Should_Return_Exactly_Items_Of_That_Module()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), scenario =>
        {
            var (items, targetModule) = scenario;

            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);
            itemRepo.AddRangeAsync(items).GetAwaiter().GetResult();

            var service = BuildService(db);
            var result = service.GetItemsByModuleAsync(targetModule).GetAwaiter().GetResult();

            // 期望集合：插入项中 Module 等于目标值（区分大小写 / 序数比较）的项 Id 集合。
            var expectedIds = items
                .Where(x => string.Equals(x.Module, targetModule, StringComparison.Ordinal))
                .Select(x => x.Id)
                .ToHashSet();

            var actualIds = result.Select(x => x.Id).ToHashSet();

            // 断言 1：返回的 Id 集合与期望集合完全相等（数量相同、Id 相同，不多不少）。
            if (!actualIds.SetEquals(expectedIds)) return false;

            // 断言 2：结果中无重复 Id。
            if (actualIds.Count != result.Count) return false;

            // 断言 3：返回的每个项 Module 均等于目标过滤值（区分大小写）。
            if (result.Any(x => !string.Equals(x.Module, targetModule, StringComparison.Ordinal))) return false;

            return true;
        }).QuickCheckThrowOnFailure();
    }
}
