// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 10：可授权入口仅含各端默认组下的项。
// 对应设计文档《Correctness Properties / Property 10》与任务 7.4，验证需求 8.5：
//   对于任意由若干默认组（IsDefault=1）与非默认组（IsDefault=0）及其菜单组项构成的数据集合，
//   MenuGroupAppService.GetGrantableItemsAsync 返回的入口项必须：
//     1) 全部隶属于某个 IsDefault=1 的默认菜单组，不含任何非默认组的项；
//     2) 返回的每个 GrantableMenuItemDto.GroupId 均对应一个 IsDefault=1 的默认组；
//     3) 返回项 Id 集合恰好等于全部默认组下的项 Id 集合（完整且互斥，不多不少）。
// 测试策略（与设计《Testing Strategy》一致）：使用 xUnit + FsCheck.Xunit（不自行实现属性测试框架），
//   单个属性测试、最少 100 次迭代；注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，
//   直接驱动应用服务真实的可授权入口聚合逻辑；含父子嵌套以校验树形展开（递归 Children）。

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
/// Property 10：可授权入口仅含各端默认组下的项。
/// </summary>
public sealed class GrantableOnlyDefaultGroupProperty
{
    /// <summary>
    /// 单个菜单组计划：
    /// - <see cref="IsDefault"/>：是否为默认组（IsDefault=1）；
    /// - <see cref="ClientType"/>：单一终端类型；
    /// - <see cref="ItemCount"/>：该组下的菜单组项数量（含 0，覆盖空组边界）；
    /// - <see cref="Nested"/>：项是否构成父子嵌套链（true=链式嵌套，false=同级平铺），
    ///   用于校验返回树形展开后仍覆盖该组全部项（递归 Children）。
    /// </summary>
    private sealed record GroupPlan(bool IsDefault, string ClientType, int ItemCount, bool Nested);

    // Feature: multi-client-plugin-portal, Property 10: 可授权入口仅含各端默认组下的项
    /// <summary>
    /// Property 10：GetGrantableItemsAsync 返回的入口项均隶属某 IsDefault=1 默认组、
    /// 不含任何非默认组项；且返回项 Id 集合恰等于全部默认组下的项 Id 集合（完整且互斥）。
    /// **Validates: Requirements 8.5**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void GrantableItems_Should_Only_Contain_Default_Group_Items()
    {
        Prop.ForAll(GroupPlansGen().ToArbitrary(), plans =>
        {
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);

            // 默认组 Id 集合 / 默认组项 Id 集合 / 非默认组项 Id 集合，作为断言期望。
            var defaultGroupIds = new HashSet<long>();
            var defaultItemIds = new HashSet<long>();
            var nonDefaultItemIds = new HashSet<long>();

            // 1. 按计划播种菜单组及其项（默认组与非默认组混合，含多个默认组与多种终端类型）。
            foreach (var plan in plans)
            {
                var seed = SnowflakeIdGenerator.NextId();
                var group = MenuGroup.Create(
                    name: "菜单组-" + seed,
                    slug: "group-" + seed,
                    clientType: plan.ClientType,
                    isSystem: plan.IsDefault,
                    isDefault: plan.IsDefault);
                groupRepo.AddAsync(group).GetAwaiter().GetResult();
                if (plan.IsDefault) defaultGroupIds.Add(group.Id);

                long? previousId = null;
                for (int i = 0; i < plan.ItemCount; i++)
                {
                    var item = MenuGroupItem.Create(
                        menuGroupId: group.Id,
                        title: "入口项-" + i,
                        linkType: "Custom",
                        url: "/item/" + group.Id + "/" + i,
                        module: "Ginkgo.Module.Demo",
                        requireGrant: i % 2 == 0);
                    // 嵌套场景：从第二个项起挂到上一项之下，形成链式父子，校验树形递归展开。
                    if (plan.Nested && previousId.HasValue)
                    {
                        item.MoveTo(previousId.Value);
                    }
                    item.SetOrder(i);
                    itemRepo.AddAsync(item).GetAwaiter().GetResult();
                    previousId = item.Id;

                    if (plan.IsDefault) defaultItemIds.Add(item.Id);
                    else nonDefaultItemIds.Add(item.Id);
                }
            }

            // 2. 调用被测方法获取可授权入口。
            var service = BuildService(db);
            var grantable = service.GetGrantableItemsAsync().GetAwaiter().GetResult();

            // 3. 展开所有返回组的树形项，收集返回的入口项 Id。
            var returnedIds = new HashSet<long>();
            foreach (var group in grantable)
            {
                // 断言 2（需求 8.5）：每个返回组的 GroupId 必须对应一个默认组。
                if (!defaultGroupIds.Contains(group.GroupId)) return false;
                CollectIds(group.Items, returnedIds);
            }

            // 断言 1（需求 8.5）：返回项不得包含任何非默认组的项。
            if (returnedIds.Overlaps(nonDefaultItemIds)) return false;

            // 断言 3（需求 8.5）：返回项 Id 集合恰等于全部默认组下的项 Id 集合（完整且互斥）。
            if (!returnedIds.SetEquals(defaultItemIds)) return false;

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 递归展开可授权入口树，收集所有节点 Id。
    /// </summary>
    private static void CollectIds(List<GrantableItemNodeDto>? nodes, HashSet<long> sink)
    {
        if (nodes == null) return;
        foreach (var node in nodes)
        {
            sink.Add(node.Id);
            CollectIds(node.Children, sink);
        }
    }

    /// <summary>
    /// 生成菜单组计划集合（含空集合）：0~6 个菜单组，默认/非默认混合、终端类型随机、各带 0~5 个项、随机是否嵌套。
    /// </summary>
    private static Gen<List<GroupPlan>> GroupPlansGen()
    {
        return Gen.Choose(0, 6).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<GroupPlan>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => GroupPlanGen()))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单个菜单组计划：随机默认标识、单一终端类型、0~5 个项、随机是否嵌套。
    /// </summary>
    private static Gen<GroupPlan> GroupPlanGen()
    {
        return from isDefault in PortalGenerators.Bool()
               from clientType in PortalGenerators.SingleClientType()
               from itemCount in Gen.Choose(0, 5)
               from nested in PortalGenerators.Bool()
               select new GroupPlan(isDefault, clientType, itemCount, nested);
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
