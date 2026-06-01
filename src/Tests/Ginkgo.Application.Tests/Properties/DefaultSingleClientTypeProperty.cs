// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试 —— Property 2：默认标识要求单一终端类型。
// 本文件仅实现设计文档《Correctness Properties》中编号为 2 的单条正确性属性，
// 验证 MenuGroupAppService.SetGroupDefaultAsync 对「单一终端 / 多终端」目标组的处理：
//   - 当目标组 ClientType 表示单一终端类型（无逗号、非空）时，设为默认应成功，目标组 IsDefault 变为 true；
//   - 当目标组 ClientType 含逗号分隔的多个终端类型（或为空）时，操作应一律被拒绝并抛出说明性
//     InvalidOperationException，且不改变库中任何菜单组的 IsDefault 标识。

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
/// Property 2：默认标识要求单一终端类型。
/// </summary>
public sealed class DefaultSingleClientTypeProperty
{
    // Feature: multi-client-plugin-portal, Property 2: 默认标识要求单一终端类型
    /// <summary>
    /// 对于任意 ClientType 字符串：仅当其表示单一终端类型时「设为默认」才成功（目标组 IsDefault 变为 true）；
    /// 凡含逗号分隔的多个终端类型时操作一律被拒绝并抛出说明性错误，且不改变任何组的 IsDefault。
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void SetGroupDefault_Should_Require_Single_ClientType()
    {
        // 随机 ClientType：可能是单端（UNIAPP/WEB_PORTAL/WPF）或逗号分隔的多端。
        Prop.ForAll(PortalGenerators.AnyClientType().ToArbitrary(), clientType =>
        {
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);
            var service = BuildService(db);

            // 1. 播种目标组：使用随机生成的 ClientType，初始 IsDefault=0。
            var target = MenuGroup.Create(
                name: "目标菜单组",
                slug: "target-" + SnowflakeIdGenerator.NextId(),
                clientType: clientType,
                isSystem: false,
                isDefault: false);
            groupRepo.AddAsync(target).GetAwaiter().GetResult();

            // 2. 播种若干其他组（含不同 ClientType 与 IsDefault 取值），用于校验拒绝路径不改动任何组。
            var others = new List<MenuGroup>
            {
                MenuGroup.Create("其他组A", "other-a-" + SnowflakeIdGenerator.NextId(),
                    clientType: PortalClientTypes.Uniapp, isDefault: true),
                MenuGroup.Create("其他组B", "other-b-" + SnowflakeIdGenerator.NextId(),
                    clientType: PortalClientTypes.WebPortal, isDefault: false),
                MenuGroup.Create("其他组C", "other-c-" + SnowflakeIdGenerator.NextId(),
                    clientType: PortalClientTypes.Wpf, isDefault: true),
            };
            foreach (var other in others)
            {
                groupRepo.AddAsync(other).GetAwaiter().GetResult();
            }

            // 3. 记录调用前所有组的 IsDefault 快照（用于拒绝路径比对）。
            var before = groupRepo.Query().ToList().ToDictionary(x => x.Id, x => x.IsDefault);

            // 4. 与服务一致地判定「单一终端 vs 多终端」：按逗号拆分并移除空项后，恰好 1 个即为单一终端。
            var segments = clientType.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var isSingle = segments.Length == 1;

            if (isSingle)
            {
                // 单一终端：设为默认应成功，目标组 IsDefault 变为 true。
                service.SetGroupDefaultAsync(target.Id).GetAwaiter().GetResult();
                var reloaded = groupRepo.GetByIdAsync(target.Id).GetAwaiter().GetResult();
                return reloaded != null && reloaded.IsDefault;
            }

            // 多终端：操作应被拒绝并抛 InvalidOperationException，且不改变任何组的 IsDefault。
            var threwExpected = false;
            try
            {
                service.SetGroupDefaultAsync(target.Id).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                threwExpected = true;
            }

            var after = groupRepo.Query().ToList().ToDictionary(x => x.Id, x => x.IsDefault);
            var unchanged = before.Count == after.Count
                && before.All(kv => after.TryGetValue(kv.Key, out var v) && v == kv.Value);

            return threwExpected && unchanged;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 构造被测的 <see cref="MenuGroupAppService"/>：其构造函数需要 8 个仓储，
    /// 均以内存仓储基于同一内存测试库注入，保证服务内部各查询访问同一份数据。
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
