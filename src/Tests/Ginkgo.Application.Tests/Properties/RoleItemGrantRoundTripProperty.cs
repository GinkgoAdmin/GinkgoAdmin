// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 9：角色项级授权全量覆盖且唯一（set/get 往返）。
// 对应设计文档《Correctness Properties / Property 9》与任务 7.3，验证需求 8.2、8.3、8.4：
//   对于任意角色与任意提交的菜单组项 Id 集合，执行「设置角色项授权」后再查询该角色已授权项 Id 集合，
//   结果等于提交集合去重后的集合（全量覆盖，需求 8.3/8.4）；
//   同一 RoleId 与同一 MenuGroupItemId 不存在重复授权记录（唯一，需求 8.2）。
// 测试策略（与设计《Testing Strategy》一致）：使用 FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，直接驱动应用服务真实的读写逻辑：
//   MenuGroupAppService.SetRoleMenuGroupItemsAsync（全量覆盖去重）与 GetRoleMenuGroupItemIdsAsync（查询已授权 Id）。
//
// 关键约定：
//   - 每次迭代先用一组随机「初始授权集合」预置该角色（验证全量覆盖而非合并，需求 8.3）；
//     再用「提交集合」覆盖，断言 get 结果等于提交集合去重结果（与初始集合无关）。
//   - 提交集合从一个较小的项 Id 池（1..10）生成，刻意制造重复与空集合，覆盖「去重」「清空」边界。
//   - 同时预置另一角色（OtherRoleId）的授权集合，验证对目标角色的覆盖不影响其他角色（授权隔离）。
//   - 底层 RoleMenuGroupItem 行按 (RoleId, MenuGroupItemId) 分组，断言每组计数为 1（无重复授权记录，需求 8.2）。

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
/// Property 9：角色项级授权全量覆盖且唯一（set/get 往返）。
/// </summary>
public sealed class RoleItemGrantRoundTripProperty
{
    /// <summary>
    /// 用于授权隔离对照的「其他角色」Id（取一个与目标角色 Id 池 [1,100] 不相交的值）。
    /// </summary>
    private const long OtherRoleId = 999_999L;

    /// <summary>
    /// 测试场景计划：
    /// - <see cref="RoleId"/>：目标角色 Id（取自 [1,100]，确保与 <see cref="OtherRoleId"/> 不相交）；
    /// - <see cref="InitialItemIds"/>：注入前预置给目标角色的初始授权集合（验证全量覆盖而非合并）；
    /// - <see cref="SubmittedItemIds"/>：本次提交的菜单组项 Id 集合（含重复 / 空集合）；
    /// - <see cref="OtherRoleItemIds"/>：预置给其他角色的授权集合（验证覆盖目标角色时其不受影响）。
    /// </summary>
    private sealed record ScenarioPlan(
        long RoleId,
        IReadOnlyList<long> InitialItemIds,
        IReadOnlyList<long> SubmittedItemIds,
        IReadOnlyList<long> OtherRoleItemIds);

    // Feature: multi-client-plugin-portal, Property 9: 角色项级授权全量覆盖且唯一（set/get 往返）
    /// <summary>
    /// Property 9：对于任意角色与任意提交集合，预置初始授权后以提交集合全量覆盖，再查询该角色已授权项：
    ///   1) get 结果集合等于提交集合去重后的集合（需求 8.3/8.4）；
    ///   2) get 结果本身无重复（需求 8.2）；
    ///   3) 底层 RoleMenuGroupItem 行按 (RoleId, MenuGroupItemId) 分组每组计数为 1（无重复授权记录，需求 8.2）；
    ///   4) 全量覆盖不影响其他角色的授权（授权隔离）。
    /// **Validates: Requirements 8.2, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void SetThenGet_Should_Equal_DistinctSubmitted_And_Have_No_Duplicate_Grants()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), plan =>
        {
            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var grantRepo = new InMemoryRepository<RoleMenuGroupItem>(db);
            var service = BuildService(db);

            // 1. 预置其他角色的授权集合（用于验证覆盖目标角色时其不受影响）。
            service.SetRoleMenuGroupItemsAsync(
                new SetRoleMenuGroupItemsInput
                {
                    RoleId = OtherRoleId,
                    MenuGroupItemIds = plan.OtherRoleItemIds.ToList()
                }).GetAwaiter().GetResult();

            // 2. 预置目标角色的「初始授权集合」（验证后续为全量覆盖而非合并）。
            service.SetRoleMenuGroupItemsAsync(
                new SetRoleMenuGroupItemsInput
                {
                    RoleId = plan.RoleId,
                    MenuGroupItemIds = plan.InitialItemIds.ToList()
                }).GetAwaiter().GetResult();

            // 3. 以「提交集合」全量覆盖目标角色授权。
            service.SetRoleMenuGroupItemsAsync(
                new SetRoleMenuGroupItemsInput
                {
                    RoleId = plan.RoleId,
                    MenuGroupItemIds = plan.SubmittedItemIds.ToList()
                }).GetAwaiter().GetResult();

            // 4. 查询目标角色已授权项 Id 集合。
            var got = service.GetRoleMenuGroupItemIdsAsync(plan.RoleId).GetAwaiter().GetResult();

            var expected = plan.SubmittedItemIds.Distinct().ToHashSet();

            // 断言 1（需求 8.3/8.4）：get 结果集合等于提交集合去重结果（顺序无关的集合相等）。
            if (!got.ToHashSet().SetEquals(expected)) return false;

            // 断言 2（需求 8.2）：get 结果本身不含重复。
            if (got.Count != got.Distinct().Count()) return false;

            // 断言 3（需求 8.2）：底层授权行不存在重复的 (RoleId, MenuGroupItemId)。
            var hasDuplicateRows = grantRepo.Query()
                .Where(x => x.RoleId == plan.RoleId)
                .ToList()
                .GroupBy(x => new { x.RoleId, x.MenuGroupItemId })
                .Any(g => g.Count() > 1);
            if (hasDuplicateRows) return false;

            // 断言 4（授权隔离）：覆盖目标角色不影响其他角色授权，其结果仍等于其提交集合去重结果。
            var otherGot = service.GetRoleMenuGroupItemIdsAsync(OtherRoleId).GetAwaiter().GetResult();
            if (!otherGot.ToHashSet().SetEquals(plan.OtherRoleItemIds.Distinct().ToHashSet())) return false;

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 生成测试场景：随机目标角色 Id（[1,100]）、初始授权集合、提交集合、其他角色授权集合。
    /// </summary>
    private static Gen<ScenarioPlan> ScenarioGen()
    {
        return from roleId in Gen.Choose(1, 100).Select(i => (long)i)
               from initial in ItemIdListGen()
               from submitted in ItemIdListGen()
               from otherItems in ItemIdListGen()
               select new ScenarioPlan(roleId, initial, submitted, otherItems);
    }

    /// <summary>
    /// 生成菜单组项 Id 集合（含空集合与重复）：长度 0~8，取值来自较小的 Id 池 [1,10]，
    /// 以提高重复出现概率，覆盖「去重」与「清空」边界。
    /// </summary>
    private static Gen<List<long>> ItemIdListGen()
    {
        return Gen.Choose(0, 8).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<long>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => Gen.Choose(1, 10).Select(i => (long)i)))
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
