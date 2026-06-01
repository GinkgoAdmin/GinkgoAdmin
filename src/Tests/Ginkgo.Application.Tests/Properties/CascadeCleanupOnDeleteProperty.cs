// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 11：删除项/角色级联清理授权关联。
// 对应设计文档《Correctness Properties / Property 11》与任务 7.5，验证需求 8.7、7.2：
//   对于任意已存在 RoleMenuGroupItem 授权的数据集合，删除某个菜单组项（或某个角色）后，
//   与之相关的 RoleMenuGroupItem 关联记录全部被清除，不残留孤儿授权（即剩余授权均指向仍存在的角色与菜单组项）。
//
// 本属性涉及两条级联路径，均由真实生产代码执行（不模拟、不复刻清理逻辑）：
//   1) 删除菜单组项：MenuGroupAppService.DeleteItemAsync(groupId, id, ct)。
//      该方法会递归删除子项，并级联清理被删项（及其子孙项）对应的 RoleMenuGroupItem（需求 8.7）。
//   2) 删除角色：RoleAppService.DeleteAsync(id, ct)。
//      该方法先级联清理该角色的 RoleMenuGroupItem，再删除角色（需求 8.7、7.2）。
//
// 关于构造 RoleAppService：其构造函数依赖较多，但 DeleteAsync 仅使用
//   IRepository<RoleMenuGroupItem> 与 IRepository<Role> 两个仓储；其余依赖（IRoleDataScopeService、
//   IRoleRepository、IRolePermissionRepository、IMenuRepository、IPermissionRepository）在 DeleteAsync
//   路径上不会被调用。因此为这些依赖提供轻量桩实现（仅返回安全默认值，永不参与删除逻辑），
//   即可在测试中调用真实的 RoleAppService.DeleteAsync 产生级联效果，避免「复刻清理逻辑」导致的失真。
//
// 测试策略（与设计《Testing Strategy》一致）：使用 xUnit + FsCheck.Xunit（不自行实现属性测试框架），
//   单个属性测试、最少 100 次迭代；注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，
//   直接驱动应用服务真实的删除/级联清理逻辑。
//
// 生成器覆盖边界：空集合、单个/多个角色、单个/多个菜单组项、父子/根节点树形结构、
//   随机授权关系（含指向删除目标与不指向删除目标的混合授权）、随机选择「删项」或「删角色」的删除目标。

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Roles;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Permissions;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// Property 11：删除项/角色级联清理授权关联。
/// </summary>
public sealed class CascadeCleanupOnDeleteProperty
{
    /// <summary>删除目标类型。</summary>
    private enum DeleteKind
    {
        /// <summary>删除某个菜单组项（经 <see cref="MenuGroupAppService.DeleteItemAsync"/> 递归级联）。</summary>
        Item,

        /// <summary>删除某个角色（经 <see cref="RoleAppService.DeleteAsync"/> 级联）。</summary>
        Role
    }

    /// <summary>
    /// 单个菜单组项的结构计划（以索引表达父子关系，运行期再映射为真实雪花 Id）：
    /// - <see cref="IsRoot"/>：是否为根节点（ParentId=null）；
    /// - <see cref="ParentPick"/>：非根节点时，用于在前序项 [0, index) 中选取父节点。
    /// </summary>
    private sealed record ItemPlan(bool IsRoot, int ParentPick);

    /// <summary>
    /// 授权计划（以索引表达）：第 <see cref="RolePick"/> 个角色 对 第 <see cref="ItemPick"/> 个菜单组项授权。
    /// 运行期按角色数 / 菜单组项数取模映射为真实 Id，并去重以保证 (RoleId, MenuGroupItemId) 唯一。
    /// </summary>
    private sealed record GrantPlan(int RolePick, int ItemPick);

    /// <summary>
    /// 测试场景计划：
    /// - <see cref="ClientType"/>：承载菜单组项的菜单组所属终端类型（单一终端类型）；
    /// - <see cref="Items"/>：菜单组项结构计划（构成森林，可为空集合）；
    /// - <see cref="RoleCount"/>：角色数量（可为 0）；
    /// - <see cref="Grants"/>：授权关系计划（可为空集合）；
    /// - <see cref="Kind"/>：本次删除的目标类型（删项 / 删角色）；
    /// - <see cref="TargetPick"/>：用于在目标集合中按取模选择具体删除目标。
    /// </summary>
    private sealed record ScenarioPlan(
        string ClientType,
        IReadOnlyList<ItemPlan> Items,
        int RoleCount,
        IReadOnlyList<GrantPlan> Grants,
        DeleteKind Kind,
        int TargetPick);

    // Feature: multi-client-plugin-portal, Property 11: 删除项/角色级联清理授权关联
    /// <summary>
    /// Property 11：对于任意已存在授权的数据集合，删除某个菜单组项或某个角色后：
    ///   1) 与被删菜单组项（含其子孙项）相关、或与被删角色相关的 RoleMenuGroupItem 关联记录全部被清除（需求 8.7/7.2）；
    ///   2) 未涉及删除目标的授权关系完全保留、未被波及（无过度删除）；
    ///   3) 删除后不残留孤儿授权（剩余每条 RoleMenuGroupItem 均指向仍存在的角色与菜单组项）。
    /// **Validates: Requirements 8.7, 7.2**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void DeleteItemOrRole_Should_Cascade_Cleanup_Grants_Without_Orphans()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), plan =>
        {
            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);
            var roleRepo = new InMemoryRepository<Role>(db);
            var grantRepo = new InMemoryRepository<RoleMenuGroupItem>(db);

            // 1. 预置承载菜单组项的菜单组。
            var group = MenuGroup.Create(
                name: "组-" + SnowflakeIdGenerator.NextId(),
                slug: "g-" + SnowflakeIdGenerator.NextId(),
                clientType: plan.ClientType,
                isSystem: false,
                isDefault: true);
            group.Enable();
            groupRepo.AddAsync(group).GetAwaiter().GetResult();

            // 2. 按结构计划创建菜单组项，构成森林（每个非根项的父节点取自前序项，保证无环）。
            var itemIds = new List<long>();
            var parentIndexOf = new int?[plan.Items.Count]; // 记录每个项的父索引（null=根），用于运行期计算子树
            var items = new List<MenuGroupItem>();
            for (var i = 0; i < plan.Items.Count; i++)
            {
                long? parentId = null;
                int? parentIndex = null;
                if (i > 0 && !plan.Items[i].IsRoot)
                {
                    parentIndex = plan.Items[i].ParentPick % i; // 取前序项作为父，保证非自引用、无环
                    parentId = items[parentIndex.Value].Id;
                }
                parentIndexOf[i] = parentIndex;

                var item = MenuGroupItem.Create(
                    menuGroupId: group.Id,
                    title: "项-" + i,
                    linkType: "Custom",
                    url: "/p/" + i,
                    parentId: parentId,
                    module: "Ginkgo.Module.Demo",
                    requireGrant: true);
                items.Add(item);
                itemRepo.AddAsync(item).GetAwaiter().GetResult();
                itemIds.Add(item.Id);
            }

            // 3. 创建角色。
            var roleIds = new List<long>();
            for (var i = 0; i < plan.RoleCount; i++)
            {
                var seed = SnowflakeIdGenerator.NextId();
                var role = new Role
                {
                    Id = seed,
                    Name = "角色-" + seed,
                    Code = "role-" + seed,
                    Enabled = true
                };
                roleRepo.AddAsync(role).GetAwaiter().GetResult();
                roleIds.Add(role.Id);
            }

            // 4. 按授权计划写入 RoleMenuGroupItem（去重保证 (RoleId, MenuGroupItemId) 唯一）。
            //    仅当存在角色与菜单组项时才可能产生授权。
            var grantPairs = new HashSet<(long RoleId, long ItemId)>();
            if (roleIds.Count > 0 && itemIds.Count > 0)
            {
                foreach (var g in plan.Grants)
                {
                    var roleId = roleIds[g.RolePick % roleIds.Count];
                    var itemId = itemIds[g.ItemPick % itemIds.Count];
                    grantPairs.Add((roleId, itemId));
                }
            }
            foreach (var (roleId, itemId) in grantPairs)
            {
                grantRepo.AddAsync(new RoleMenuGroupItem
                {
                    Id = SnowflakeIdGenerator.NextId(),
                    RoleId = roleId,
                    MenuGroupItemId = itemId,
                    CreatedAt = System.DateTime.Now
                }).GetAwaiter().GetResult();
            }

            // 5. 依据删除目标类型执行删除，并计算「预期被清除的授权」与「删除后仍存活的实体」。
            HashSet<(long RoleId, long ItemId)> expectedRemaining;
            if (plan.Kind == DeleteKind.Item)
            {
                if (itemIds.Count == 0)
                {
                    // 无菜单组项可删：删除不发生，授权应原样保留（空操作下属性平凡成立）。
                    return GrantSet(grantRepo).SetEquals(grantPairs);
                }

                var targetIndex = plan.TargetPick % itemIds.Count;

                // 计算被删项的整棵子树（含自身），即真实 DeleteItemAsync 递归删除的项集合。
                var deletedItemIndexes = CollectSubtree(targetIndex, parentIndexOf);
                var deletedItemIds = deletedItemIndexes.Select(i => itemIds[i]).ToHashSet();

                // 预期清除：授权项落在被删子树内；预期保留：其余授权。
                expectedRemaining = grantPairs.Where(p => !deletedItemIds.Contains(p.ItemId)).ToHashSet();

                // 执行真实删除（递归 + 级联清理 RoleMenuGroupItem）。
                var service = BuildMenuGroupService(db);
                service.DeleteItemAsync(group.Id, itemIds[targetIndex]).GetAwaiter().GetResult();
            }
            else
            {
                if (roleIds.Count == 0)
                {
                    // 无角色可删：删除不发生，授权应原样保留。
                    return GrantSet(grantRepo).SetEquals(grantPairs);
                }

                var targetRoleId = roleIds[plan.TargetPick % roleIds.Count];

                // 预期清除：该角色的全部授权；预期保留：其他角色授权。
                expectedRemaining = grantPairs.Where(p => p.RoleId != targetRoleId).ToHashSet();

                // 执行真实删除（级联清理该角色的 RoleMenuGroupItem，再删角色）。
                var roleService = BuildRoleService(db);
                roleService.DeleteAsync(targetRoleId).GetAwaiter().GetResult();
            }

            // 6. 断言。
            var remaining = GrantSet(grantRepo);

            // 断言 1（需求 8.7/7.2 + 无过度删除）：剩余授权恰好等于预期保留集合
            //   —— 即与删除目标相关的授权全部清除，且未涉及目标的授权完整保留。
            if (!remaining.SetEquals(expectedRemaining)) return false;

            // 断言 2（无孤儿授权）：剩余每条授权均指向仍存在的角色与菜单组项。
            var survivingRoleIds = roleRepo.Query().Select(x => x.Id).ToList().ToHashSet();
            var survivingItemIds = itemRepo.Query().Select(x => x.Id).ToList().ToHashSet();
            if (remaining.Any(p => !survivingRoleIds.Contains(p.RoleId) || !survivingItemIds.Contains(p.ItemId)))
            {
                return false;
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 读取当前内存库中全部 RoleMenuGroupItem 授权，归约为 (RoleId, MenuGroupItemId) 集合，便于集合比较。
    /// </summary>
    private static HashSet<(long RoleId, long ItemId)> GrantSet(InMemoryRepository<RoleMenuGroupItem> grantRepo)
        => grantRepo.Query().ToList().Select(x => (x.RoleId, x.MenuGroupItemId)).ToHashSet();

    /// <summary>
    /// 依据父索引数组计算指定节点的整棵子树索引集合（含自身），等价于 DeleteItemAsync 的递归删除范围。
    /// </summary>
    private static HashSet<int> CollectSubtree(int rootIndex, int?[] parentIndexOf)
    {
        // 预先按父索引构建「父 → 子列表」邻接表。
        var children = new Dictionary<int, List<int>>();
        for (var i = 0; i < parentIndexOf.Length; i++)
        {
            var p = parentIndexOf[i];
            if (p.HasValue)
            {
                if (!children.TryGetValue(p.Value, out var list))
                {
                    list = new List<int>();
                    children[p.Value] = list;
                }
                list.Add(i);
            }
        }

        var result = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(rootIndex);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!result.Add(cur)) continue;
            if (children.TryGetValue(cur, out var list))
            {
                foreach (var c in list) stack.Push(c);
            }
        }
        return result;
    }

    /// <summary>
    /// 生成测试场景：随机单一终端类型、菜单组项结构（0~8 项的森林）、角色数量（0~5）、
    /// 授权关系计划（0~12 条）、删除目标类型与目标选择索引。
    /// </summary>
    private static Gen<ScenarioPlan> ScenarioGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from items in ItemPlanListGen()
               from roleCount in Gen.Choose(0, 5)
               from grants in GrantPlanListGen()
               from kind in Gen.Elements(DeleteKind.Item, DeleteKind.Role)
               from targetPick in Gen.Choose(0, 1000)
               select new ScenarioPlan(clientType, items, roleCount, grants, kind, targetPick);
    }

    /// <summary>
    /// 生成菜单组项结构计划集合（含空集合）：0~8 项，每项随机决定是否为根，以及非根时的父选择。
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
    /// 生成单个菜单组项结构计划：约 1/3 概率为根节点，否则携带父选择索引。
    /// </summary>
    private static Gen<ItemPlan> ItemPlanGen()
    {
        return from rootRoll in Gen.Choose(0, 2) // 1/3 概率为根
               from parentPick in Gen.Choose(0, 1000)
               select new ItemPlan(rootRoll == 0, parentPick);
    }

    /// <summary>
    /// 生成授权关系计划集合（含空集合）：0~12 条，索引取自较大范围后在运行期取模映射。
    /// </summary>
    private static Gen<List<GrantPlan>> GrantPlanListGen()
    {
        return Gen.Choose(0, 12).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<GrantPlan>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => GrantPlanGen()))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单条授权关系计划（角色选择索引 + 菜单组项选择索引）。
    /// </summary>
    private static Gen<GrantPlan> GrantPlanGen()
    {
        return from rolePick in Gen.Choose(0, 1000)
               from itemPick in Gen.Choose(0, 1000)
               select new GrantPlan(rolePick, itemPick);
    }

    /// <summary>
    /// 以内存仓储装配 <see cref="MenuGroupAppService"/>（构造函数所需 8 个仓储均基于同一内存库）。
    /// </summary>
    private static MenuGroupAppService BuildMenuGroupService(InMemoryTestDatabase db)
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
    /// 以内存仓储 + 轻量桩依赖装配真实的 <see cref="RoleAppService"/>。
    /// DeleteAsync 仅使用 IRepository&lt;RoleMenuGroupItem&gt; 与 IRepository&lt;Role&gt;，
    /// 其余依赖在删除路径上不会被调用，故以仅返回安全默认值的桩实现注入。
    /// </summary>
    private static RoleAppService BuildRoleService(InMemoryTestDatabase db)
    {
        return new RoleAppService(
            new InMemoryRepository<Role>(db),
            new InMemoryRepository<Permission>(db),
            new InMemoryRepository<RolePermission>(db),
            new InMemoryRepository<Menu>(db),
            new InMemoryRepository<RoleMenuGroupItem>(db),
            new StubRoleDataScopeService(),
            new StubRoleRepository(),
            new StubRolePermissionRepository(),
            new StubMenuRepository(),
            new StubPermissionRepository());
    }

    // ===== 轻量桩依赖（仅用于装配 RoleAppService；删除路径上不会被调用） =====

    /// <summary>角色数据范围服务桩：返回空映射、空操作替换。</summary>
    private sealed class StubRoleDataScopeService : IRoleDataScopeService
    {
        public Task<IReadOnlyList<long>> GetSpecifiedDepartmentIdsAsync(long roleId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<long>>(new List<long>());

        public Task ReplaceAsync(Role role, string normalizedDataScope, IEnumerable<long> departmentIds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>角色查询仓储桩：返回空分页与空列表。</summary>
    private sealed class StubRoleRepository : IRoleRepository
    {
        public Task<(long total, List<Role> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
            => Task.FromResult<(long, List<Role>)>((0, new List<Role>()));

        public Task<List<Role>> GetAllOrderedAsync(CancellationToken ct = default)
            => Task.FromResult(new List<Role>());
    }

    /// <summary>角色-权限关系仓储桩：返回空集合、空操作替换。</summary>
    private sealed class StubRolePermissionRepository : IRolePermissionRepository
    {
        public Task<List<long>> GetAssignedPermissionIdsAsync(long roleId, CancellationToken ct = default)
            => Task.FromResult(new List<long>());

        public Task ReplaceAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>菜单仓储桩：返回空列表。</summary>
    private sealed class StubMenuRepository : IMenuRepository
    {
        public Task<List<Menu>> GetAllOrderedAsync(CancellationToken ct = default)
            => Task.FromResult(new List<Menu>());
    }

    /// <summary>权限仓储桩：返回空列表。</summary>
    private sealed class StubPermissionRepository : IPermissionRepository
    {
        public Task<List<Permission>> GetAllEnabledAsync(CancellationToken ct = default)
            => Task.FromResult(new List<Permission>());
    }
}
