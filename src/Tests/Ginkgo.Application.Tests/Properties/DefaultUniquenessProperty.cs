// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 1：默认菜单组每端唯一。
// 对应设计文档《Correctness Properties / Property 1》与任务 4.2，验证需求 1.2、1.3：
//   将某个单一终端类型的菜单组「设为默认」后，目标组所属终端类型下有且仅有该目标组 IsDefault=1，
//   同端其他组的 IsDefault 全部被重置为 0，其余终端类型的菜单组 IsDefault 不受影响。
// 测试策略（与设计《Testing Strategy》一致）：使用 FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，直接驱动 MenuGroupAppService.SetGroupDefaultAsync。

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
/// 默认菜单组每端唯一性属性测试。
/// </summary>
public sealed class DefaultUniquenessProperty
{
    /// <summary>
    /// 生成单一终端类型的菜单组：ClientType 取单端常量（UNIAPP/WEB_PORTAL/WPF），随机初始 IsDefault 与 Enabled。
    /// 仅产出单端组，保证目标组可被合法「设为默认」（多端组会被服务拒绝，不属于本属性的取样范围）。
    /// </summary>
    private static Gen<MenuGroup> SingleTerminalGroupGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from isDefault in PortalGenerators.Bool()
               from enabled in PortalGenerators.Bool()
               select BuildGroup(clientType, isDefault, enabled);
    }

    /// <summary>
    /// 生成测试场景：至少 1 个、至多 6 个单端菜单组，以及目标组选择索引。
    /// 覆盖单组、同端多组、跨端混合、初始已有/未有默认组等情形。
    /// </summary>
    private static Gen<(List<MenuGroup> Groups, int TargetPick)> ScenarioGen()
    {
        return Gen.Choose(1, 6).SelectMany(count =>
            Gen.Sequence(Enumerable.Range(0, count).Select(_ => SingleTerminalGroupGen()))
                .SelectMany(seq =>
                {
                    var groups = seq.ToList();
                    return Gen.Choose(0, groups.Count - 1)
                        .Select(pick => (groups, pick));
                }));
    }

    private static MenuGroup BuildGroup(string clientType, bool isDefault, bool enabled)
    {
        var seed = SnowflakeIdGenerator.NextId();
        var group = MenuGroup.Create(
            name: "菜单组-" + seed,
            slug: "group-" + seed,
            clientType: clientType,
            isSystem: false,
            isDefault: isDefault);
        if (enabled) group.Enable(); else group.Disable();
        return group;
    }

    /// <summary>
    /// 以内存仓储装配 <see cref="MenuGroupAppService"/>（构造需 8 个仓储依赖）。
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

    // Feature: multi-client-plugin-portal, Property 1: 默认菜单组每端唯一
    /// <summary>
    /// Property 1：对于任意单端菜单组集合与任一被选为默认的目标组，执行「设为默认」后，
    /// 目标组所属终端类型下有且仅有该目标组 IsDefault=1，同端其他组被重置为 0，其余终端类型不受影响。
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void DefaultMenuGroup_Should_Be_Unique_Per_ClientType()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), scenario =>
        {
            var (groups, targetPick) = scenario;
            var target = groups[targetPick % groups.Count];
            var targetId = target.Id;
            // 种子组使用精确常量（UNIAPP/WEB_PORTAL/WPF），按序数相等比较即可表示同端归属
            var targetClientType = target.ClientType!;

            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            foreach (var g in groups)
            {
                groupRepo.AddAsync(g).GetAwaiter().GetResult();
            }

            // 调用前快照：记录每个组的初始 IsDefault，用于校验其余终端类型不受影响
            var snapshot = groups.ToDictionary(g => g.Id, g => g.IsDefault);

            var service = BuildService(db);
            service.SetGroupDefaultAsync(targetId).GetAwaiter().GetResult();

            var reloaded = groupRepo.Query().ToList();

            // 断言 1：目标组 IsDefault == true
            var reloadedTarget = reloaded.First(x => x.Id == targetId);
            if (!reloadedTarget.IsDefault) return false;

            // 断言 2：同端组中恰有一个 IsDefault=1，且为目标组
            var sameTypeDefaults = reloaded
                .Where(x => string.Equals(x.ClientType, targetClientType, StringComparison.Ordinal))
                .Where(x => x.IsDefault)
                .ToList();
            if (sameTypeDefaults.Count != 1) return false;
            if (sameTypeDefaults[0].Id != targetId) return false;

            // 断言 3：其余终端类型的组 IsDefault 与调用前一致（不受影响）
            foreach (var g in reloaded.Where(x =>
                         !string.Equals(x.ClientType, targetClientType, StringComparison.Ordinal)))
            {
                if (snapshot[g.Id] != g.IsDefault) return false;
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }
}
