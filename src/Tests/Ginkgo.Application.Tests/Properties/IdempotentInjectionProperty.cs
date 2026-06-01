// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 6：重复注入幂等（upsert 不产生重复）。
// 本文件仅实现设计文档《Correctness Properties / Property 6》对应的单条正确性属性，验证需求 6.4：
//   对于任意一组 ClientMenus 入口声明，连续注入两次的结果与注入一次等价：
//     - 按入口标识 (MenuGroupId, Module, Url) 不产生重复项；
//     - 第二次注入后各项的 Id 与字段值与第一次注入后完全一致（更新已存在项、不新增多余项）。
// 测试策略（与设计《Testing Strategy》一致）：使用 FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，直接驱动 MenuGroupAppService.UpsertClientMenuItemsAsync。
// 采用「单库注入两次再对比」方案：先注入一次并快照，再注入一次后重取并断言完全一致，
//   直接验证「二次注入与一次等价 + 无重复」。

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
// 区分测试基础设施侧的 ClientMenuItemSpec（输入模型）与应用层注入用的 ClientMenuItemSpec（方法入参）。
using AppClientMenuItemSpec = Ginkgo.Application.Menus.ClientMenuItemSpec;
using TestClientMenuItemSpec = Ginkgo.Application.Tests.Infrastructure.ClientMenuItemSpec;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// Property 6：重复注入幂等（upsert 不产生重复）。
/// </summary>
public sealed class IdempotentInjectionProperty
{
    /// <summary>
    /// 菜单组项字段快照：用于跨两次注入对比 Id 与关键字段是否完全一致。
    /// 采用值元组的结构化相等语义进行比较。
    /// </summary>
    private static (long Id, long? ParentId, long MenuGroupId, string Module, string? Url,
        string Title, string? Icon, bool RequireGrant, int Order, string? Badge, string LinkType)
        Snapshot(MenuGroupItem x)
        => (x.Id, x.ParentId, x.MenuGroupId, x.Module, x.Url,
            x.Title, x.Icon, x.RequireGrant, x.Order, x.Badge, x.LinkType);

    /// <summary>
    /// 生成测试场景：单一终端类型 + 模块归属（含 'sys' 与若干插件 Id）+ 一组入口声明（含空集合与重复 path）。
    /// </summary>
    private static Gen<(string ClientType, string ModuleId, List<TestClientMenuItemSpec> Specs)> ScenarioGen()
    {
        return from clientType in PortalGenerators.SingleClientType()
               from moduleId in PortalGenerators.Module()
               from specs in PortalGenerators.ClientMenuItemSpecListGen()
               select (clientType, moduleId, specs);
    }

    // Feature: multi-client-plugin-portal, Property 6: 重复注入幂等（upsert 不产生重复）
    /// <summary>
    /// Property 6：对于任意一组 ClientMenus 声明，连续注入两次与注入一次等价：
    /// 按 (MenuGroupId, Module, Url) 标识不产生重复项，且第二次注入后各项 Id 与字段值与第一次完全一致。
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void RepeatedInjection_Should_Be_Idempotent_Without_Duplicates()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), scenario =>
        {
            var (clientType, moduleId, testSpecs) = scenario;

            // 每次迭代使用独立内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var itemRepo = new InMemoryRepository<MenuGroupItem>(db);

            // 1. 预置该端的默认菜单组（IsDefault=1），作为入口注入目标。
            var group = MenuGroup.Create(
                name: "默认组-" + clientType,
                slug: "default-" + SnowflakeIdGenerator.NextId(),
                clientType: clientType,
                isSystem: true,
                isDefault: true);
            groupRepo.AddAsync(group).GetAwaiter().GetResult();

            var service = BuildService(db);

            // 2. 将测试输入映射为应用层注入规格（字段一一对应）。
            var appSpecs = testSpecs.Select(s => new AppClientMenuItemSpec
            {
                Title = s.Title,
                Icon = s.Icon,
                Path = s.Path,
                RequireGrant = s.RequireGrant,
                Order = s.Order,
                Badge = s.Badge
            }).ToList();

            // 该模块归属的归一化值（与服务内部一致：去空白，空则视为 'sys'）。
            var normalizedModule = string.IsNullOrWhiteSpace(moduleId) ? "sys" : moduleId.Trim();

            // 3. 第一次注入并快照（仅本组、本模块的入口项）。
            service.UpsertClientMenuItemsAsync(clientType, moduleId, appSpecs).GetAwaiter().GetResult();
            var afterFirst = itemRepo.Query()
                .Where(x => x.MenuGroupId == group.Id && x.Module == normalizedModule)
                .ToList();
            var firstSnapshot = afterFirst.Select(Snapshot).OrderBy(t => t.Id).ToList();

            // 4. 第二次注入（同一组声明）。
            service.UpsertClientMenuItemsAsync(clientType, moduleId, appSpecs).GetAwaiter().GetResult();
            var afterSecond = itemRepo.Query()
                .Where(x => x.MenuGroupId == group.Id && x.Module == normalizedModule)
                .ToList();

            // 断言 1：按入口标识 (MenuGroupId, Module, Url) 不存在重复项。
            var hasDuplicate = afterSecond
                .GroupBy(x => (x.MenuGroupId, x.Module, x.Url))
                .Any(g => g.Count() > 1);
            if (hasDuplicate) return false;

            // 断言 2：项数等于声明中去重后的 path 数（重复 path 收敛为单项；空集合为 0）。
            var distinctPathCount = testSpecs
                .Select(s => s.Path?.Trim())
                .Distinct()
                .Count();
            if (afterFirst.Count != distinctPathCount) return false;
            if (afterSecond.Count != distinctPathCount) return false;

            // 断言 3：第二次注入结果与第一次完全等价（Id 稳定、字段值一致）。
            var secondSnapshot = afterSecond.Select(Snapshot).OrderBy(t => t.Id).ToList();
            if (firstSnapshot.Count != secondSnapshot.Count) return false;
            for (var i = 0; i < firstSnapshot.Count; i++)
            {
                if (!firstSnapshot[i].Equals(secondSnapshot[i])) return false;
            }

            return true;
        }).QuickCheckThrowOnFailure();
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
