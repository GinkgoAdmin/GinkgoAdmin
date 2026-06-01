// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 5：入口仅注入到对应端的默认菜单组。
// 对应设计文档《Correctness Properties / Property 5》与任务 6.5，验证需求 6.1、6.2、6.5：
//   对于任意合法 clientType 的入口声明，经 MenuGroupAppService.UpsertClientMenuItemsAsync 注入后：
//     1) 入口项全部落在该 clientType 的 IsDefault=1 默认菜单组下（需求 6.1）；
//     2) 任何 IsDefault=0 的非默认菜单组其项数量保持不变（需求 6.2）；
//     3) 当该 clientType 不存在默认组时，不创建任何 MenuGroupItem（需求 6.5）。
// 测试策略（与设计《Testing Strategy》一致）：使用 FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，直接驱动应用服务真实注入逻辑。
// 说明：测试基础设施侧的 ClientMenuItemSpec 与应用层 ClientMenuItemSpec 同名但分属不同命名空间，
//   故在注入前需将测试侧规格映射为应用层规格（UpsertClientMenuItemsAsync 接受应用层类型）。

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
using AppSpec = Ginkgo.Application.Menus.ClientMenuItemSpec;
using InfraSpec = Ginkgo.Application.Tests.Infrastructure.ClientMenuItemSpec;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// Property 5：入口仅注入到对应端的默认菜单组。
/// </summary>
public sealed class InjectOnlyDefaultGroupProperty
{
    /// <summary>注入所用的插件模块标识（区分大小写）。</summary>
    private const string InjectionModule = "Ginkgo.Module.Demo";

    /// <summary>
    /// 非默认组预置项所用的模块集合（刻意排除注入模块 <see cref="InjectionModule"/>），
    /// 以保证「Module==注入模块」的项必定来源于本次注入，便于断言入口归属。
    /// </summary>
    private static readonly string[] PreExistingModules =
    {
        "sys",
        "Ginkgo.Module.SmartCommunity",
        "Ginkgo.Module.Evaluate"
    };

    /// <summary>
    /// 待播种的非默认菜单组计划：终端类型（可能与目标端相同或不同）+ 预置项数量。
    /// </summary>
    private sealed record NonDefaultGroupPlan(string ClientType, int ItemCount);

    /// <summary>
    /// 测试场景计划：
    /// - <see cref="ClientType"/>：目标端（单一终端类型）；
    /// - <see cref="HasDefaultGroup"/>：是否存在该端默认组（true=场景 A，false=场景 B）；
    /// - <see cref="Specs"/>：待注入的客户端入口声明（测试基础设施侧类型）；
    /// - <see cref="NonDefaultGroups"/>：若干非默认组（含同端 / 其他端，各带预置项）。
    /// </summary>
    private sealed record ScenarioPlan(
        string ClientType,
        bool HasDefaultGroup,
        IReadOnlyList<InfraSpec> Specs,
        IReadOnlyList<NonDefaultGroupPlan> NonDefaultGroups);

    // Feature: multi-client-plugin-portal, Property 5: 入口仅注入到对应端的默认菜单组
    /// <summary>
    /// Property 5：对于任意合法 clientType 的入口声明，注入后入口项全部落在该 clientType 的
    /// IsDefault=1 菜单组下；任何 IsDefault=0 的非默认菜单组其项数量保持不变；当该 clientType
    /// 不存在默认组时，不创建任何 MenuGroupItem。
    /// **Validates: Requirements 6.1, 6.2, 6.5**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Inject_Should_Only_Target_Default_Group()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), plan =>
        {
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);

            // 1. 场景 A：播种恰好一个该端默认组（IsDefault=1）；场景 B：不播种任何默认组。
            long? defaultGroupId = null;
            if (plan.HasDefaultGroup)
            {
                var defaultGroup = MenuGroup.Create(
                    name: "默认菜单组-" + plan.ClientType,
                    slug: "default-" + SnowflakeIdGenerator.NextId(),
                    clientType: plan.ClientType,
                    isSystem: true,
                    isDefault: true);
                groupRepo.AddAsync(defaultGroup).GetAwaiter().GetResult();
                defaultGroupId = defaultGroup.Id;
            }

            // 2. 播种若干非默认组（IsDefault=0），含同端 / 其他端，各带预置项（模块均不等于注入模块）。
            var nonDefaultGroupIds = new List<long>();
            foreach (var nd in plan.NonDefaultGroups)
            {
                var seed = SnowflakeIdGenerator.NextId();
                var group = MenuGroup.Create(
                    name: "非默认菜单组-" + seed,
                    slug: "nd-" + seed,
                    clientType: nd.ClientType,
                    isSystem: false,
                    isDefault: false);
                groupRepo.AddAsync(group).GetAwaiter().GetResult();
                nonDefaultGroupIds.Add(group.Id);

                for (int i = 0; i < nd.ItemCount; i++)
                {
                    var module = PreExistingModules[i % PreExistingModules.Length];
                    var item = MenuGroupItem.Create(
                        menuGroupId: group.Id,
                        title: "预置项-" + i,
                        linkType: "Custom",
                        url: "/pre/" + group.Id + "/" + i,
                        module: module,
                        requireGrant: false);
                    itemRepo.AddAsync(item).GetAwaiter().GetResult();
                }
            }

            // 3. 非默认组项数快照（用于校验注入后非默认组项数不变）。
            var snapshot = nonDefaultGroupIds.ToDictionary(
                gid => gid,
                gid => itemRepo.Query().Where(x => x.MenuGroupId == gid).ToList().Count);

            // 4. 将测试侧入口声明映射为应用层规格后执行注入。
            var appSpecs = plan.Specs.Select(s => new AppSpec
            {
                Title = s.Title,
                Icon = s.Icon,
                Path = s.Path,
                RequireGrant = s.RequireGrant,
                Order = s.Order,
                Badge = s.Badge
            }).ToList();

            var service = BuildService(db);
            service.UpsertClientMenuItemsAsync(plan.ClientType, InjectionModule, appSpecs)
                .GetAwaiter().GetResult();

            // 注入产生的项即库中 Module==注入模块 的项（预置项模块刻意排除注入模块）。
            var injected = itemRepo.Query().Where(x => x.Module == InjectionModule).ToList();

            if (plan.HasDefaultGroup)
            {
                // 断言 1（需求 6.1）：注入项全部落在该端默认组下。
                if (injected.Any(x => x.MenuGroupId != defaultGroupId!.Value)) return false;
            }
            else
            {
                // 断言 3（需求 6.5）：无默认组时不创建任何 MenuGroupItem。
                if (injected.Count != 0) return false;
            }

            // 断言 2（需求 6.2）：任何非默认组其项数量保持不变。
            foreach (var gid in nonDefaultGroupIds)
            {
                var nowCount = itemRepo.Query().Where(x => x.MenuGroupId == gid).ToList().Count;
                if (nowCount != snapshot[gid]) return false;
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 生成测试场景：随机目标端（单一终端类型）、是否存在默认组、待注入声明集合（含空集合 / 重复 path）、
    /// 以及 0~4 个非默认组（同端或其他端，各带 0~4 个预置项）。
    /// </summary>
    private static Gen<ScenarioPlan> ScenarioGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from hasDefault in PortalGenerators.Bool()
               from specs in PortalGenerators.ClientMenuItemSpecListGen()
               from nonDefaults in NonDefaultGroupsGen()
               select new ScenarioPlan(clientType, hasDefault, specs, nonDefaults);
    }

    /// <summary>
    /// 生成非默认组计划集合（含空集合）。
    /// </summary>
    private static Gen<List<NonDefaultGroupPlan>> NonDefaultGroupsGen()
    {
        return Gen.Choose(0, 4).SelectMany(count =>
            count == 0
                ? Gen.Constant(new List<NonDefaultGroupPlan>())
                : Gen.Sequence(Enumerable.Range(0, count).Select(_ => NonDefaultGroupPlanGen()))
                    .Select(seq => seq.ToList()));
    }

    /// <summary>
    /// 生成单个非默认组计划：随机单一终端类型（可能与目标端相同或不同）+ 0~4 个预置项。
    /// </summary>
    private static Gen<NonDefaultGroupPlan> NonDefaultGroupPlanGen()
    {
        return from ct in PortalGenerators.SingleClientType()
               from itemCount in Gen.Choose(0, 4)
               select new NonDefaultGroupPlan(ct, itemCount);
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
