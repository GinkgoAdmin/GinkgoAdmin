// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）冒烟/示例测试 —— 任务 18.2：
//   预置三组与「系统内置不可删除」示例测试。
// 本文件为 xUnit 示例（[Fact]）测试（非属性测试），用具体示例断言以下两项：
//   1) 在全新空内存库上执行框架真实预置逻辑后，恰好创建三端系统级默认菜单组，且字段正确
//      （需求 4.1/4.2/4.3：默认移动端/默认WEB前台/默认桌面端，均 IsDefault=1、IsSystem=1；
//        需求 4.6：三组归属主框架数据 'sys'，MenuGroup 无 Module 列，以 IsSystem=1 体现系统/框架归属）。
//   2) 删除 IsSystem=1 的系统内置菜单组被拒绝（需求 4.4），抛 InvalidOperationException 且该组保留。
//
// 关键约束（与任务 12.2 一致）：本测试不复刻预置逻辑，而是通过反射调用主框架真实生产代码
//   Ginkgo.Api.Bootstrap.DatabaseMaintenanceService.EnsureDefaultMenuGroups(ISqlSugarClient)（私有静态方法）。
//   删除拒绝逻辑则直接调用真实应用服务 MenuGroupAppService.DeleteGroupAsync（注入 8 个内存仓储）。

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ginkgo.Api.Bootstrap;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Permissions;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using Xunit;

namespace Ginkgo.Application.Tests.Examples;

/// <summary>
/// 预置三端默认菜单组与「系统内置不可删除」示例测试（任务 18.2）。
/// </summary>
public sealed class DefaultMenuGroupPresetExampleTests
{
    /// <summary>
    /// 三端系统级默认菜单组的期望字段（与生产代码 EnsureDefaultMenuGroups 中保持一致）。
    /// 元组：名称、Slug（唯一）、终端类型。
    /// </summary>
    private static readonly (string Name, string Slug, string ClientType)[] ExpectedPresets =
    {
        ("默认移动端", "default-uniapp", "UNIAPP"),
        ("默认WEB前台", "default-web-portal", "WEB_PORTAL"),
        ("默认桌面端", "default-wpf", "WPF"),
    };

    /// <summary>
    /// 反射定位生产代码中的私有静态预置方法，确保示例测试验证的是真实生产逻辑而非复制实现。
    /// </summary>
    private static readonly MethodInfo EnsureMethod =
        typeof(DatabaseMaintenanceService).GetMethod(
            "EnsureDefaultMenuGroups",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "未能通过反射定位 DatabaseMaintenanceService.EnsureDefaultMenuGroups(ISqlSugarClient) 方法。");

    /// <summary>
    /// 通过反射调用真实生产预置逻辑；将反射包装异常解包为内部真实异常，便于失败时定位。
    /// </summary>
    private static void InvokeEnsure(InMemoryTestDatabase db)
    {
        try
        {
            EnsureMethod.Invoke(null, new object[] { db.Client });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
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
    /// 在全新空内存库上执行真实预置逻辑后，应恰好创建三端系统级默认菜单组，且字段正确。
    /// 断言每组的 Name/Slug/ClientType/IsDefault/IsSystem 与规格一致。
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.6**
    /// </summary>
    [Fact]
    public void EnsureDefaultMenuGroups_Should_Create_Three_System_Default_Groups()
    {
        // 全新空内存库，仅执行一次预置逻辑。
        using var db = new InMemoryTestDatabase();
        var groupRepo = new InMemoryRepository<MenuGroup>(db);

        // 前置确认：空库无任何菜单组。
        Assert.Empty(groupRepo.Query().ToList());

        // 执行真实生产预置逻辑。
        InvokeEnsure(db);

        var groups = groupRepo.Query().ToList();

        // 恰好创建三组（不多不少）。
        Assert.Equal(3, groups.Count);

        // 逐组断言字段正确（需求 4.1/4.2/4.3；4.6 以 IsSystem=1 体现系统/框架归属）。
        foreach (var (name, slug, clientType) in ExpectedPresets)
        {
            var group = groups.SingleOrDefault(g => g.Slug == slug);
            Assert.NotNull(group);
            Assert.Equal(name, group!.Name);
            Assert.Equal(slug, group.Slug);
            Assert.Equal(clientType, group.ClientType);
            Assert.True(group.IsDefault, $"预置组 {slug} 应为默认菜单组（IsDefault=1）。");
            Assert.True(group.IsSystem, $"预置组 {slug} 应为系统内置菜单组（IsSystem=1）。");
        }
    }

    /// <summary>
    /// 删除 IsSystem=1 的系统内置菜单组应被拒绝：抛 InvalidOperationException("系统内置菜单组不可删除")，
    /// 且该菜单组在删除尝试后依然存在（需求 4.4）。
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Fact]
    public async Task DeleteGroup_Should_Reject_System_Builtin_Group()
    {
        using var db = new InMemoryTestDatabase();
        var groupRepo = new InMemoryRepository<MenuGroup>(db);

        // 预置一个系统内置默认组（取预置之一：默认移动端）。
        var systemGroup = MenuGroup.Create(
            name: "默认移动端",
            slug: "default-uniapp",
            clientType: "UNIAPP",
            isSystem: true,
            isDefault: true);
        await groupRepo.AddAsync(systemGroup);

        var service = BuildMenuGroupService(db);

        // 删除系统内置组应抛出指定异常。
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteGroupAsync(systemGroup.Id));
        Assert.Equal("系统内置菜单组不可删除", ex.Message);

        // 删除被拒后，该组仍然存在。
        var stillExists = await groupRepo.GetByIdAsync(systemGroup.Id);
        Assert.NotNull(stillExists);
        Assert.True(stillExists!.IsSystem);
    }
}
