// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 3：注入写入正确的模块归属与字段映射。
// 对应设计文档《Correctness Properties / Property 3》与任务 6.3，验证需求 2.3、6.3、11.4、12.3：
//   对于任意插件 moduleId 与任意一组合法 ClientMenus 入口声明，注入后产生的每个 MenuGroupItem 的
//   Module 恒等于该 moduleId（区分大小写），且 Title/Icon/Url/RequireGrant/Order/Badge 分别等于
//   声明的 title/icon/path/requireGrant/order/badge。
// 测试策略（与设计《Testing Strategy》一致）：使用 FsCheck.Xunit，单个属性测试、最少 100 次迭代，
//   注入内存仓储（基于 SQLite 内存库），不 mock SqlSugar，直接驱动 MenuGroupAppService.UpsertClientMenuItemsAsync。
//
// 关键约定：
//   - 存在两个同名 ClientMenuItemSpec：测试基础设施记录（InfraSpec，供 PortalGenerators 生成，覆盖空集合/重复 path 等边界）
//     与应用层规格类（AppSpec，UpsertClientMenuItemsAsync 的真实入参类型）。本测试以 InfraSpec 生成后映射为 AppSpec 调用注入。
//   - 入口标识为 (MenuGroupId, Module, Url)，重复 path 会 upsert 到同一项，最终字段以输入序列中“最后一条同 path 声明”为准（顺序写入、后写覆盖）。
//   - 注入仅在该端存在 IsDefault=1 默认组时发生，故每个场景先为所选单端类型预置一个默认组。

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
using InfraSpec = Ginkgo.Application.Tests.Infrastructure.ClientMenuItemSpec;
using AppSpec = Ginkgo.Application.Menus.ClientMenuItemSpec;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// 注入字段映射属性测试。
/// </summary>
public sealed class InjectionFieldMappingProperty
{
    /// <summary>
    /// 生成测试场景：随机插件 moduleId（含 'sys' 与若干插件 Id）、单一终端类型、一组客户端入口声明（含空集合与重复 path）。
    /// </summary>
    private static Gen<(string ModuleId, string ClientType, List<InfraSpec> Specs)> ScenarioGen()
    {
        return from moduleId in PortalGenerators.Module()
               from clientType in PortalGenerators.SingleClientType()
               from specs in PortalGenerators.ClientMenuItemSpecListGen()
               select (moduleId, clientType, specs);
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

    /// <summary>
    /// 预置某单端类型的默认菜单组（IsDefault=1、IsSystem=1、Enabled=1），作为入口注入目标。
    /// </summary>
    private static MenuGroup SeedDefaultGroup(InMemoryTestDatabase db, string clientType)
    {
        var groupRepo = new InMemoryRepository<MenuGroup>(db);
        var seed = SnowflakeIdGenerator.NextId();
        var group = MenuGroup.Create(
            name: "默认组-" + seed,
            slug: "default-" + seed,
            clientType: clientType,
            isSystem: true,
            isDefault: true);
        group.Enable();
        groupRepo.AddAsync(group).GetAwaiter().GetResult();
        return group;
    }

    /// <summary>
    /// 将测试基础设施记录映射为应用层规格类，作为 UpsertClientMenuItemsAsync 的真实入参。
    /// </summary>
    private static List<AppSpec> ToAppSpecs(List<InfraSpec> specs)
    {
        return specs.Select(s => new AppSpec
        {
            Title = s.Title,
            Icon = s.Icon,
            Path = s.Path,
            RequireGrant = s.RequireGrant,
            Order = s.Order,
            Badge = s.Badge
        }).ToList();
    }

    /// <summary>
    /// 序数（区分大小写）字符串比较，安全处理 null（两者皆 null 视为相等）。
    /// </summary>
    private static bool OrdinalEquals(string? a, string? b) => string.Equals(a, b, StringComparison.Ordinal);

    // Feature: multi-client-plugin-portal, Property 3: 注入写入正确的模块归属与字段映射
    /// <summary>
    /// Property 3：对于任意 moduleId 与任意一组合法 ClientMenus 入口声明，注入后产生的每个 MenuGroupItem 的
    /// Module 恒等于该 moduleId（区分大小写），且 Title/Icon/Url/RequireGrant/Order/Badge 分别等于
    /// 声明的 title/icon/path/requireGrant/order/badge（重复 path 以最后一条同 path 声明为准）。
    /// **Validates: Requirements 2.3, 6.3, 11.4, 12.3**
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Injection_Should_Write_Correct_Module_And_Field_Mapping()
    {
        Prop.ForAll(ScenarioGen().ToArbitrary(), scenario =>
        {
            var (moduleId, clientType, specs) = scenario;
            var appSpecs = ToAppSpecs(specs);

            using var db = new InMemoryTestDatabase();
            SeedDefaultGroup(db, clientType);
            var service = BuildService(db);

            // 执行注入
            service.UpsertClientMenuItemsAsync(clientType, moduleId, appSpecs).GetAwaiter().GetResult();

            // 查询该模块注入后的全部入口项（区分大小写按 Module 过滤）
            var injected = service.GetItemsByModuleAsync(moduleId).GetAwaiter().GetResult();

            // 期望项数：去重 path（按 Url=path 序数比较）后的数量。
            var distinctPaths = appSpecs
                .Select(s => s.Path?.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (injected.Count != distinctPaths.Count) return false;

            foreach (var item in injected)
            {
                // 模块归属恒等于 moduleId（区分大小写）
                if (!OrdinalEquals(item.Module, moduleId)) return false;

                // 链接类型固定为 Custom
                if (!OrdinalEquals(item.LinkType, "Custom")) return false;

                // 定位与该项 Url(path) 对应的“最后一条”声明（顺序写入、后写覆盖）
                var expected = appSpecs.LastOrDefault(s => OrdinalEquals(s.Path?.Trim(), item.Url));
                if (expected == null) return false;

                // 字段映射断言：实现对各字段统一做 Trim 处理，期望值同样取 Trim 后比较
                if (!OrdinalEquals(item.Title, expected.Title?.Trim() ?? string.Empty)) return false;
                if (!OrdinalEquals(item.Icon, expected.Icon?.Trim())) return false;
                if (!OrdinalEquals(item.Url, expected.Path?.Trim())) return false;
                if (!OrdinalEquals(item.Badge, expected.Badge?.Trim())) return false;
                if (item.RequireGrant != expected.RequireGrant) return false;
                if (item.Order != expected.Order) return false;
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }
}
