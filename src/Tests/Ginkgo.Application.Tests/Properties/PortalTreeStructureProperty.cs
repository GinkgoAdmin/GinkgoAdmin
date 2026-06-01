// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 14：入口树结构与排序正确。
// 对应设计文档《Correctness Properties / Property 14》与任务 9.3，验证需求 9.5：
//   对于任意可见菜单组项集合，MenuGroupAppService.GetClientPortalAsync 构建的入口树必须满足：
//     1) 结构正确：每个非根节点（即挂在某父节点 Children 下的节点）的 ParentId 恒等于其父节点的 Id；
//     2) 根节点 ParentId 为 null：在「合法森林（所有父引用都在集合内）+ 超管全部可见」的前提下，
//        所有根节点的 ParentId 必为 null（不存在父项不可见而被提升为根的情形）；
//     3) 排序正确：每一层级（根列表以及任意节点的 Children 列表）内的节点均按 Order 升序（非降序）排列；
//     4) 完整无损：入口树中全部节点 Id 集合恰等于播种的全部菜单组项 Id 集合（不丢失、不重复）。
// 测试策略（与设计《Testing Strategy》一致）：使用 xUnit + FsCheck.Xunit（不自行实现属性测试框架），
//   单个属性测试、最少 100 次迭代；注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，
//   直接驱动应用服务真实的可见性判定与树构建逻辑。
//
// 关键场景构造：
//   - 仅播种一个「单一终端类型」的默认菜单组（IsDefault=1、Enabled=1），所有入口项落在该组下；
//   - 入口项构成「合法森林」：第 0 项始终为根（ParentId=null）；对 i>0 项，要么作为根，
//     要么以前序已创建项之一作为父节点，从而保证不产生环、且每个非根 ParentId 都指向集合内的项；
//   - 所有入口项 Enable（即便非超管也可见），并播种一个「超管用户」（Role.IsSuperAdmin=true + UserRole 关联），
//     使 GetClientPortalAsync 返回该组下「全部」入口项，便于在完整集合上干净地校验结构与排序。

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
/// Property 14：入口树结构与排序正确。
/// </summary>
public sealed class PortalTreeStructureProperty
{
    /// <summary>
    /// 单个入口项计划：
    /// - <see cref="Order"/>：排序号（含负数与乱序，校验同级 Order 升序）；
    /// - <see cref="ForceRoot"/>：是否强制作为根节点（ParentId=null）；
    /// - <see cref="ParentPick"/>：非根时用于在前序已创建项中选取父节点的随机数（取模定位，保证合法森林）；
    /// - <see cref="Module"/>：模块归属（对结构/排序无影响，仅增加数据多样性）；
    /// - <see cref="RequireGrant"/>：是否需授权（超管全部可见，对本属性无影响，仅增加多样性）。
    /// </summary>
    private sealed record ItemPlan(int Order, bool ForceRoot, int ParentPick, string Module, bool RequireGrant);

    /// <summary>
    /// 场景计划：单一终端类型的默认组 + 一组入口项计划（含空集合）。
    /// </summary>
    private sealed record ScenarioPlan(string ClientType, List<ItemPlan> Items);

    // Feature: multi-client-plugin-portal, Property 14: 入口树结构与排序正确
    /// <summary>
    /// Property 14：对于任意可见菜单组项集合，构建的入口树满足结构（非根节点 ParentId 指向其父）、
    /// 根节点 ParentId 为 null、同级 Order 升序、以及完整无损（节点集合恰等于播种集合）。
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Portal_Tree_Should_Have_Correct_Structure_And_Ordering()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), plan =>
        {
            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);
            var roleRepo = new InMemoryRepository<Role>(db);
            var userRoleRepo = new InMemoryRepository<UserRole>(db);

            // 1. 播种该端唯一默认菜单组（单一终端类型、IsDefault=1、Enabled=1）。
            var group = MenuGroup.Create(
                name: "默认组-" + plan.ClientType,
                slug: "default-" + plan.ClientType.ToLowerInvariant(),
                clientType: plan.ClientType,
                isSystem: true,
                isDefault: true);
            group.Enable();
            groupRepo.AddAsync(group).GetAwaiter().GetResult();

            // 2. 按计划播种入口项，构造「合法森林」：第 0 项为根，i>0 项要么为根、要么以前序项为父。
            var createdItems = new List<MenuGroupItem>(plan.Items.Count);
            var seededIds = new HashSet<long>();
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var ip = plan.Items[i];
                var item = MenuGroupItem.Create(
                    menuGroupId: group.Id,
                    title: "入口项-" + i,
                    linkType: "Custom",
                    url: "/item/" + group.Id + "/" + i,
                    module: ip.Module,
                    requireGrant: ip.RequireGrant);
                item.SetOrder(ip.Order);
                item.Enable();

                // 父节点指派：保证合法森林（无环、所有父引用都指向集合内的前序项）。
                if (i == 0 || ip.ForceRoot)
                {
                    item.MoveTo(null);
                }
                else
                {
                    var parentIndex = ip.ParentPick % i; // 取前序项作为父，保证非自引用且不成环
                    item.MoveTo(createdItems[parentIndex].Id);
                }

                itemRepo.AddAsync(item).GetAwaiter().GetResult();
                createdItems.Add(item);
                seededIds.Add(item.Id);
            }

            // 3. 播种超管用户（Role.IsSuperAdmin=true + UserRole 关联），使 portal 返回组下全部项。
            var userId = SnowflakeIdGenerator.NextId();
            var superRole = new Role
            {
                Id = SnowflakeIdGenerator.NextId(),
                Name = "超级管理员-" + userId,
                Code = "super-" + userId,
                Enabled = true,
                IsSuperAdmin = true
            };
            roleRepo.AddAsync(superRole).GetAwaiter().GetResult();
            userRoleRepo.AddAsync(new UserRole
            {
                Id = SnowflakeIdGenerator.NextId(),
                UserId = userId,
                RoleId = superRole.Id
            }).GetAwaiter().GetResult();

            // 4. 调用被测方法构建入口树。
            var service = BuildService(db);
            var portal = service.GetClientPortalAsync(plan.ClientType, userId).GetAwaiter().GetResult();
            var roots = portal.Items;

            // 断言 2（需求 9.5）：合法森林 + 超管全部可见，所有根节点 ParentId 必为 null。
            if (roots.Any(r => r.ParentId != null)) return false;

            // 断言 3（需求 9.5）：根列表按 Order 升序（非降序）。
            if (!IsNonDecreasing(roots)) return false;

            // 断言 1 + 3（需求 9.5）：递归校验每个子节点的 ParentId 指向其父、且每个 Children 列表 Order 升序。
            if (!CheckSubtree(roots)) return false;

            // 断言 4（需求 9.5）：收集入口树全部节点 Id，校验与播种集合完整一致（不丢失、不重复）。
            var collected = new List<long>();
            CollectIds(roots, collected);
            if (collected.Count != seededIds.Count) return false;             // 无重复（计数一致）
            if (!collected.ToHashSet().SetEquals(seededIds)) return false;     // 无丢失（集合一致）

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 递归校验子树：对每个节点，其 Children 列表须按 Order 升序，
    /// 且每个子节点的 ParentId 必等于当前节点的 Id（结构正确）。
    /// </summary>
    private static bool CheckSubtree(List<ClientPortalItemDto> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children == null) continue;
            if (!IsNonDecreasing(node.Children)) return false;
            foreach (var child in node.Children)
            {
                if (child.ParentId != node.Id) return false;
            }
            if (!CheckSubtree(node.Children)) return false;
        }
        return true;
    }

    /// <summary>
    /// 判断同级节点列表是否按 Order 非降序排列。
    /// </summary>
    private static bool IsNonDecreasing(List<ClientPortalItemDto> nodes)
    {
        for (int i = 1; i < nodes.Count; i++)
        {
            if (nodes[i].Order < nodes[i - 1].Order) return false;
        }
        return true;
    }

    /// <summary>
    /// 递归收集入口树全部节点 Id（含子节点）。
    /// </summary>
    private static void CollectIds(List<ClientPortalItemDto> nodes, List<long> sink)
    {
        foreach (var node in nodes)
        {
            sink.Add(node.Id);
            if (node.Children != null) CollectIds(node.Children, sink);
        }
    }

    /// <summary>
    /// 生成测试场景：随机单一终端类型 + 一组入口项计划（含空集合）。
    /// </summary>
    private static Gen<ScenarioPlan> ScenarioGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from items in ItemPlanListGen()
               select new ScenarioPlan(clientType, items);
    }

    /// <summary>
    /// 生成入口项计划集合（含空集合）：0~8 个入口项计划。
    /// </summary>
    private static Gen<List<ItemPlan>> ItemPlanListGen()
    {
        return Gen.Choose(0, 8).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<ItemPlan>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => ItemPlanGen()))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单个入口项计划：随机 Order（含负数/乱序）、约 1/3 概率强制为根、随机父选择、随机 Module 与 RequireGrant。
    /// </summary>
    private static Gen<ItemPlan> ItemPlanGen()
    {
        return from order in PortalGenerators.OrderNo()
               from rootRoll in Gen.Choose(0, 2)        // 1/3 概率强制为根
               from parentPick in Gen.Choose(0, 1000)
               from module in PortalGenerators.Module()
               from requireGrant in PortalGenerators.Bool()
               select new ItemPlan(order, rootRoll == 0, parentPick, module, requireGrant);
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
