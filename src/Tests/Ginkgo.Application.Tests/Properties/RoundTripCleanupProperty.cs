// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 7：按模块清理使 install/uninstall 往返归零。
// 对应设计文档《Correctness Properties / Property 7》与任务 6.7，验证需求 7.1、7.5：
//   对于任意初始菜单组项基线与任意一组某插件的入口注入，执行「注入后再按该插件 Module 清理」后：
//     1) 库中不存在 Module 等于该插件 Id 的残留 MenuGroupItem（需求 7.5/7.1）；
//     2) 数据恢复到注入前的基线（针对该模块）—— 即所有 Module 不等于该插件 Id 的项集合
//        （Id 与关键字段）与注入前完全一致，其他模块的数据未被波及。
// 测试策略（与设计《Testing Strategy》一致）：使用 FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，直接驱动应用服务真实的注入 / 清理逻辑：
//   MenuGroupAppService.UpsertClientMenuItemsAsync（注入）与 RemoveClientMenuItemsByModuleAsync（按模块清理）。
//
// 关键约定：
//   - 注入所用插件模块固定为 PluginModule（"Ginkgo.Module.Demo"）；基线预置项仅使用其他模块
//     （"sys" / "Ginkgo.Module.SmartCommunity" / "Ginkgo.Module.Evaluate"），刻意排除 PluginModule，
//     从而保证「Module==PluginModule」的项必定来源于本次注入，便于断言往返归零与基线恢复。
//   - 基线项可分布在该端默认菜单组以及若干非默认菜单组中（覆盖 Module 比较区分大小写、混合归属）。
//   - 清理仅删除 Module==PluginModule 的项，绝不触碰其他模块项与任何 MenuGroup 记录，因此往返后
//     非该模块项集合应与注入前基线完全相等。
//   - 测试基础设施侧 ClientMenuItemSpec 与应用层 ClientMenuItemSpec 同名但分属不同命名空间，注入前需做映射。

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
using AppSpec = Ginkgo.Application.Menus.ClientMenuItemSpec;
using InfraSpec = Ginkgo.Application.Tests.Infrastructure.ClientMenuItemSpec;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// Property 7：按模块清理使 install/uninstall 往返归零。
/// </summary>
public sealed class RoundTripCleanupProperty
{
    /// <summary>本次注入所用的插件模块标识（区分大小写）。</summary>
    private const string PluginModule = "Ginkgo.Module.Demo";

    /// <summary>
    /// 基线预置项所用模块集合（刻意排除 <see cref="PluginModule"/>），
    /// 以保证 Module==PluginModule 的项必定来源于本次注入。
    /// </summary>
    private static readonly string[] BaselineModules =
    {
        "sys",
        "Ginkgo.Module.SmartCommunity",
        "Ginkgo.Module.Evaluate"
    };

    /// <summary>
    /// 基线预置项计划：
    /// - <see cref="GroupSlot"/>：归属的菜单组槽位（0=该端默认组，1..N=非默认组）；
    /// - 其余为该项的模块归属与展示字段。
    /// </summary>
    private sealed record BaselineItemPlan(
        int GroupSlot,
        string Module,
        string Title,
        string Url,
        bool RequireGrant,
        int Order,
        string? Icon,
        string? Badge,
        bool Enabled);

    /// <summary>
    /// 测试场景计划：
    /// - <see cref="ClientType"/>：目标端（单一终端类型，用于该端默认组）；
    /// - <see cref="ExtraGroupCount"/>：额外的非默认菜单组数量（0~3）；
    /// - <see cref="Baseline"/>：注入前的基线预置项（分布在默认组 / 非默认组，模块均不等于 PluginModule）；
    /// - <see cref="Specs"/>：待注入的客户端入口声明（测试基础设施侧类型，含空集合 / 重复 path）。
    /// </summary>
    private sealed record ScenarioPlan(
        string ClientType,
        int ExtraGroupCount,
        IReadOnlyList<BaselineItemPlan> Baseline,
        IReadOnlyList<InfraSpec> Specs);

    /// <summary>
    /// 菜单组项快照：用于对比注入前基线与往返后「非该模块项」集合是否完全一致（结构化相等）。
    /// </summary>
    private static (long Id, long MenuGroupId, long? ParentId, string Title, string? Url, string Module,
        string? Icon, string? Badge, bool RequireGrant, int Order, bool Enabled, string LinkType)
        Snapshot(MenuGroupItem x)
        => (x.Id, x.MenuGroupId, x.ParentId, x.Title, x.Url, x.Module,
            x.Icon, x.Badge, x.RequireGrant, x.Order, x.Enabled, x.LinkType);

    // Feature: multi-client-plugin-portal, Property 7: 按模块清理使 install/uninstall 往返归零
    /// <summary>
    /// Property 7：对于任意初始基线与任意一组某插件的入口注入，执行「注入后再按该插件 Module 清理」后，
    /// 库中不存在 Module 等于该插件 Id 的残留 MenuGroupItem，且所有 Module 不等于该插件 Id 的项集合
    /// （Id 与关键字段）恢复到注入前基线（针对该模块）。
    /// **Validates: Requirements 7.1, 7.5**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void RoundTrip_InstallThenUninstall_Should_ZeroOut_Module_And_Restore_Baseline()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), plan =>
        {
            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);

            // 1. 预置该端默认菜单组（IsDefault=1、IsSystem=1、Enabled=1）作为入口注入目标，槽位 0。
            var defaultGroup = MenuGroup.Create(
                name: "默认组-" + plan.ClientType,
                slug: "default-" + Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                clientType: plan.ClientType,
                isSystem: true,
                isDefault: true);
            defaultGroup.Enable();
            groupRepo.AddAsync(defaultGroup).GetAwaiter().GetResult();

            var groupIds = new List<long> { defaultGroup.Id };

            // 2. 预置若干非默认菜单组（IsDefault=0），槽位 1..N；终端类型循环取值，均不影响注入目标定位。
            for (var i = 0; i < plan.ExtraGroupCount; i++)
            {
                var seed = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId();
                var extra = MenuGroup.Create(
                    name: "非默认组-" + seed,
                    slug: "nd-" + seed,
                    clientType: PortalClientTypes.Single[i % PortalClientTypes.Single.Length],
                    isSystem: false,
                    isDefault: false);
                extra.Enable();
                groupRepo.AddAsync(extra).GetAwaiter().GetResult();
                groupIds.Add(extra.Id);
            }

            // 3. 按基线计划预置初始菜单组项（模块均不等于 PluginModule）。
            foreach (var bp in plan.Baseline)
            {
                var groupId = groupIds[bp.GroupSlot % groupIds.Count];
                var item = MenuGroupItem.Create(
                    menuGroupId: groupId,
                    title: bp.Title,
                    linkType: "Custom",
                    url: bp.Url,
                    module: bp.Module,
                    requireGrant: bp.RequireGrant);
                item.Icon = bp.Icon;
                item.Badge = bp.Badge;
                item.SetOrder(bp.Order);
                if (bp.Enabled) item.Enable(); else item.Disable();
                itemRepo.AddAsync(item).GetAwaiter().GetResult();
            }

            // 4. 快照注入前基线（此时库中所有项的 Module 都不等于 PluginModule）。
            var baselineSnapshot = itemRepo.Query().ToList().Select(Snapshot).ToHashSet();

            var service = BuildService(db);

            // 5. 注入：将测试侧声明映射为应用层规格后，以 PluginModule 注入到该端默认组。
            var appSpecs = plan.Specs.Select(s => new AppSpec
            {
                Title = s.Title,
                Icon = s.Icon,
                Path = s.Path,
                RequireGrant = s.RequireGrant,
                Order = s.Order,
                Badge = s.Badge
            }).ToList();
            service.UpsertClientMenuItemsAsync(plan.ClientType, PluginModule, appSpecs).GetAwaiter().GetResult();

            // 6. 清理：按 PluginModule 清理（卸载链路），应删除全部该模块入口项。
            service.RemoveClientMenuItemsByModuleAsync(PluginModule).GetAwaiter().GetResult();

            // 7. 断言往返结果。
            var after = itemRepo.Query().ToList();

            // 断言 1（需求 7.5/7.1）：不存在 Module 等于插件 Id 的残留项（区分大小写）。
            if (after.Any(x => string.Equals(x.Module, PluginModule, StringComparison.Ordinal))) return false;

            // 断言 2（需求 7.1）：所有非该模块项恢复到注入前基线（Id 与关键字段完全一致、不多不少）。
            var afterNonPlugin = after
                .Where(x => !string.Equals(x.Module, PluginModule, StringComparison.Ordinal))
                .Select(Snapshot)
                .ToHashSet();
            if (!afterNonPlugin.SetEquals(baselineSnapshot)) return false;

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 生成测试场景：随机目标端（单一终端类型）、0~3 个非默认组、注入前基线项集合（含空集合，模块排除 PluginModule）、
    /// 以及待注入的客户端入口声明集合（含空集合 / 重复 path）。
    /// </summary>
    private static Gen<ScenarioPlan> ScenarioGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from extraGroupCount in Gen.Choose(0, 3)
               from baseline in BaselineListGen(extraGroupCount + 1)
               from specs in PortalGenerators.ClientMenuItemSpecListGen()
               select new ScenarioPlan(clientType, extraGroupCount, baseline, specs);
    }

    /// <summary>
    /// 生成基线预置项集合（含空集合），槽位范围为 [0, totalGroups)。
    /// </summary>
    private static Gen<List<BaselineItemPlan>> BaselineListGen(int totalGroups)
    {
        return Gen.Choose(0, 8).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<BaselineItemPlan>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => BaselineItemPlanGen(totalGroups)))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单个基线预置项：随机槽位（0=默认组）、随机非 PluginModule 的模块归属与展示字段。
    /// </summary>
    private static Gen<BaselineItemPlan> BaselineItemPlanGen(int totalGroups)
    {
        return from slot in Gen.Choose(0, Math.Max(0, totalGroups - 1))
               from module in Gen.Elements(BaselineModules)
               from title in PortalGenerators.Title()
               from url in PortalGenerators.Path()
               from requireGrant in PortalGenerators.Bool()
               from order in PortalGenerators.OrderNo()
               from icon in PortalGenerators.Icon()
               from badge in PortalGenerators.Badge()
               from enabled in PortalGenerators.Bool()
               select new BaselineItemPlan(slot, module, title, url, requireGrant, order, icon, badge, enabled);
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
