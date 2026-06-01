// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 12：统一入口可见性规则。
// 对应设计文档《Correctness Properties / Property 12》与任务 9.2，验证需求 3.2、3.3、9.6、9.7、11.5、11.6、12.4：
//   对于任意默认菜单组下的菜单组项集合与任意用户角色配置，MenuGroupAppService.GetClientPortalAsync
//   返回的可见项必须满足：
//     1) 当用户为超管时，返回该默认组下「全部」项（含 RequireGrant=1，且不论 Enabled 状态）（需求 9.6）；
//     2) 当用户为非超管时，返回「且仅返回」所有 Enabled=1 且（RequireGrant=0 或 RequireGrant=1 且
//        该项已通过 RoleMenuGroupItem 授权给当前用户角色）的项（需求 9.7）。
// 测试策略（与设计《Testing Strategy》一致）：使用 xUnit + FsCheck.Xunit（不自行实现属性测试框架），
//   单个属性测试、最少 100 次迭代；注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，
//   直接驱动应用服务真实的可见性判定与入口树构建逻辑。
//
// 关键设计：
//   - 仅播种一个该端默认组（IsDefault=1），其下播种随机数量的菜单组项（随机 Enabled / RequireGrant /
//     Order / 父子或孤儿 ParentId），覆盖空集合、乱序、树形/孤儿等边界。
//   - 用户角色场景覆盖三类：超管（持有含 IsSuperAdmin 的角色）、非超管（持若干角色）、无角色用户。
//   - 授权噪声：每个项的授权决策为「不授权 / 授权给用户持有的角色 / 仅授权给用户未持有的噪声角色」，
//     用以验证「授权给非用户角色不会让该项可见」。
//   - 期望集合由测试侧从原始播种数据独立重建（不调用被测可见性算法），与被测返回结果做集合相等断言；
//     返回入口树递归展开为 Id 集合后比较，确保树形/孤儿不丢项。

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
/// Property 12：统一入口可见性规则。
/// </summary>
public sealed class PortalVisibilityProperty
{
    /// <summary>用于查询的固定用户 Id（每次迭代使用独立内存库，固定值不会跨迭代污染）。</summary>
    private const long UserId = 88_888L;

    /// <summary>注入项所用的插件模块标识。</summary>
    private const string ItemModule = "Ginkgo.Module.Demo";

    /// <summary>
    /// 单个菜单组项计划：
    /// - <see cref="RequireGrant"/>：是否需要授权；
    /// - <see cref="Enabled"/>：是否启用；
    /// - <see cref="Order"/>：排序号（含负数 / 乱序）；
    /// - <see cref="Orphan"/>：是否为孤儿（ParentId 指向集合外的随机 Id）；
    /// - <see cref="ParentPick"/>：非孤儿时用于在前序项中选取父节点；
    /// - <see cref="GrantKind"/>：授权决策（0=不授权，1=授权给用户持有的角色，2=仅授权给噪声角色）。
    /// </summary>
    private sealed record ItemPlan(
        bool RequireGrant, bool Enabled, int Order, bool Orphan, int ParentPick, int GrantKind);

    /// <summary>
    /// 测试场景计划：
    /// - <see cref="ClientType"/>：单一终端类型；
    /// - <see cref="Items"/>：默认组下的菜单组项计划集合（含空集合）；
    /// - <see cref="RoleSupers"/>：候选角色的超管标记集合（用户按序持有其前若干个）；
    /// - <see cref="UserRoleTake"/>：用户持有的候选角色数量（0=无角色）。
    /// </summary>
    private sealed record ScenarioPlan(
        string ClientType,
        IReadOnlyList<ItemPlan> Items,
        IReadOnlyList<bool> RoleSupers,
        int UserRoleTake);

    // Feature: multi-client-plugin-portal, Property 12: 统一入口可见性规则
    /// <summary>
    /// Property 12：对于任意默认组下的菜单组项集合与任意用户角色配置：
    ///   超管返回组下全部项（含 RequireGrant=1，且不论 Enabled）；
    ///   非超管返回且仅返回所有 Enabled=1 且（RequireGrant=0 或已授权 RequireGrant=1）、
    ///   且其组内所有祖先同样满足该规则的项（祖先级联：插件根入口不可见则整棵子树隐藏）。
    /// **Validates: Requirements 3.2, 3.3, 9.6, 9.7, 11.5, 11.6, 12.4**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Portal_Visibility_Should_Follow_SuperAdmin_And_Grant_Rules()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), plan =>
        {
            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);
            var roleRepo = new InMemoryRepository<Role>(db);
            var userRoleRepo = new InMemoryRepository<UserRole>(db);
            var grantRepo = new InMemoryRepository<RoleMenuGroupItem>(db);

            // 1. 播种该端唯一默认组（IsDefault=1）。
            var group = MenuGroup.Create(
                name: "默认菜单组-" + plan.ClientType,
                slug: "default-" + SnowflakeIdGenerator.NextId(),
                clientType: plan.ClientType,
                isSystem: true,
                isDefault: true);
            groupRepo.AddAsync(group).GetAwaiter().GetResult();

            // 2. 播种默认组下的菜单组项，记录其 Enabled / RequireGrant 以便重建期望集合。
            var itemIds = new List<long>();
            var enabledMap = new Dictionary<long, bool>();
            var requireGrantMap = new Dictionary<long, bool>();
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var p = plan.Items[i];
                var item = MenuGroupItem.Create(
                    menuGroupId: group.Id,
                    title: "入口项-" + i,
                    linkType: "Custom",
                    url: "/item/" + i,
                    module: ItemModule,
                    requireGrant: p.RequireGrant);

                // 构造父子 / 孤儿关系：首项为根；孤儿指向集合外 Id；其余挂到某前序项之下。
                if (i == 0) item.MoveTo(null);
                else if (p.Orphan) item.MoveTo(SnowflakeIdGenerator.NextId());
                else item.MoveTo(itemIds[p.ParentPick % i]);

                item.SetOrder(p.Order);
                if (p.Enabled) item.Enable(); else item.Disable();

                itemRepo.AddAsync(item).GetAwaiter().GetResult();
                itemIds.Add(item.Id);
                enabledMap[item.Id] = p.Enabled;
                requireGrantMap[item.Id] = p.RequireGrant;
            }

            // 3. 播种候选角色（含随机超管标记）与一个永不分配给用户的噪声角色。
            var candidateRoleIds = new List<long>();
            var roleSuperById = new Dictionary<long, bool>();
            for (int k = 0; k < plan.RoleSupers.Count; k++)
            {
                var rid = SnowflakeIdGenerator.NextId();
                roleRepo.AddAsync(new Role
                {
                    Id = rid,
                    Name = "角色-" + rid,
                    Code = "role-" + rid,
                    Enabled = true,
                    IsSuperAdmin = plan.RoleSupers[k]
                }).GetAwaiter().GetResult();
                candidateRoleIds.Add(rid);
                roleSuperById[rid] = plan.RoleSupers[k];
            }

            var noiseRoleId = SnowflakeIdGenerator.NextId();
            roleRepo.AddAsync(new Role
            {
                Id = noiseRoleId,
                Name = "噪声角色-" + noiseRoleId,
                Code = "noise-" + noiseRoleId,
                Enabled = true,
                IsSuperAdmin = false
            }).GetAwaiter().GetResult();

            // 4. 用户持有前 UserRoleTake 个候选角色（写入 UserRole 关联）。
            var userRoleIds = candidateRoleIds.Take(plan.UserRoleTake).ToList();
            foreach (var rid in userRoleIds)
            {
                userRoleRepo.AddAsync(new UserRole
                {
                    Id = SnowflakeIdGenerator.NextId(),
                    UserId = UserId,
                    RoleId = rid
                }).GetAwaiter().GetResult();
            }

            // 5. 按项授权决策写入 RoleMenuGroupItem；仅「授权给用户持有角色」的项计入 grantedToUser。
            var grantedToUser = new HashSet<long>();
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var kind = plan.Items[i].GrantKind;
                var itemId = itemIds[i];

                if (kind == 1 && userRoleIds.Count > 0)
                {
                    grantRepo.AddAsync(new RoleMenuGroupItem
                    {
                        Id = SnowflakeIdGenerator.NextId(),
                        RoleId = userRoleIds[0],
                        MenuGroupItemId = itemId,
                        CreatedAt = DateTime.Now
                    }).GetAwaiter().GetResult();
                    grantedToUser.Add(itemId);
                }
                else if (kind == 2)
                {
                    // 噪声：授权给用户未持有的角色，不应使该项对用户可见。
                    grantRepo.AddAsync(new RoleMenuGroupItem
                    {
                        Id = SnowflakeIdGenerator.NextId(),
                        RoleId = noiseRoleId,
                        MenuGroupItemId = itemId,
                        CreatedAt = DateTime.Now
                    }).GetAwaiter().GetResult();
                }
            }

            // 6. 从原始播种数据独立重建期望可见集合（不复用被测算法）。
            var isSuperAdmin = userRoleIds.Any(rid => roleSuperById[rid]);
            HashSet<long> expected;
            if (isSuperAdmin)
            {
                // 超管：默认组下全部项（不论 Enabled / RequireGrant）。
                expected = itemIds.ToHashSet();
            }
            else
            {
                // 非超管单项基础规则：Enabled=1 且（RequireGrant=0 或 已授权 RequireGrant=1）。
                bool BaseVisible(long id) => enabledMap[id]
                    && (!requireGrantMap[id] || grantedToUser.Contains(id));

                // 祖先级联：某项最终可见 ⇔ 自身基础可见 且 其在组内的所有祖先均基础可见。
                // 父项指向集合外（孤儿）时不受祖先约束，仅按自身基础规则判定。
                // 与被测 GetClientPortalAsync 的「插件根入口不可见则整棵子树隐藏」语义一致。
                var parentOf = new Dictionary<long, long?>();
                for (int i = 0; i < plan.Items.Count; i++)
                {
                    var id = itemIds[i];
                    if (i == 0 || plan.Items[i].Orphan) parentOf[id] = null;
                    else parentOf[id] = itemIds[plan.Items[i].ParentPick % i];
                }
                var idSet = itemIds.ToHashSet();
                var effCache = new Dictionary<long, bool>();
                bool Effective(long id)
                {
                    if (effCache.TryGetValue(id, out var c)) return c;
                    effCache[id] = false;
                    var r = BaseVisible(id);
                    if (r && parentOf.TryGetValue(id, out var pid) && pid.HasValue && idSet.Contains(pid.Value))
                    {
                        r = Effective(pid.Value);
                    }
                    effCache[id] = r;
                    return r;
                }
                expected = itemIds.Where(Effective).ToHashSet();
            }

            // 7. 调用被测方法，递归展开返回入口树为 Id 集合。
            var service = BuildService(db);
            var dto = service.GetClientPortalAsync(plan.ClientType, UserId).GetAwaiter().GetResult();

            var returnedIds = new HashSet<long>();
            CollectIds(dto.Items, returnedIds);

            // 断言：返回可见项 Id 集合恰等于期望集合（不多不少）。
            return returnedIds.SetEquals(expected);
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 递归展开入口树，收集所有节点 Id。
    /// </summary>
    private static void CollectIds(List<ClientPortalItemDto>? nodes, HashSet<long> sink)
    {
        if (nodes == null) return;
        foreach (var node in nodes)
        {
            sink.Add(node.Id);
            CollectIds(node.Children, sink);
        }
    }

    /// <summary>
    /// 生成测试场景：随机单一终端类型、菜单组项计划集合、候选角色超管标记集合、用户持有角色数量。
    /// </summary>
    private static Gen<ScenarioPlan> ScenarioGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from items in ItemPlanListGen()
               from roleSupers in RoleSupersGen()
               from userRoleTake in Gen.Choose(0, roleSupers.Count)
               select new ScenarioPlan(clientType, items, roleSupers, userRoleTake);
    }

    /// <summary>
    /// 生成菜单组项计划集合（含空集合）：0~8 个项。
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
    /// 生成单个菜单组项计划：随机 RequireGrant / Enabled / Order / 父子或孤儿 / 授权决策。
    /// </summary>
    private static Gen<ItemPlan> ItemPlanGen()
    {
        return from requireGrant in PortalGenerators.Bool()
               from enabled in PortalGenerators.Bool()
               from order in PortalGenerators.OrderNo()
               from orphanRoll in Gen.Choose(0, 2) // 1/3 概率孤儿
               from parentPick in Gen.Choose(0, 1000)
               from grantKind in Gen.Choose(0, 2)   // 0=不授权 1=授权给用户角色 2=噪声角色
               select new ItemPlan(requireGrant, enabled, order, orphanRoll == 0, parentPick, grantKind);
    }

    /// <summary>
    /// 生成候选角色的超管标记集合（1~3 个角色）：覆盖含超管 / 全非超管两类。
    /// </summary>
    private static Gen<List<bool>> RoleSupersGen()
    {
        return Gen.Choose(1, 3).SelectMany(count =>
            Gen.Sequence(Enumerable.Range(0, count).Select(_ => PortalGenerators.Bool()))
                .Select(seq => seq.ToList()));
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
