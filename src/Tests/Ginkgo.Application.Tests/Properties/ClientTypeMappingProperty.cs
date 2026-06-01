// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）正确性属性测试 —— Property 13：clientType 映射确定且正确。
// 由于 clientType 归一化映射在 MenuGroupAppService 中实现为私有静态方法，无法直接调用，
// 故通过公共方法 GetDefaultGroupIdAsync(clientType) 的行为间接验证映射：
//   先为三个单一终端类型（UNIAPP / WEB_PORTAL / WPF）各预置一个 IsDefault=1 的默认菜单组，
//   再对覆盖大小写变体的外部入参（MOBILE / WPF / WEB_PORTAL）断言：
//     MOBILE→UNIAPP 组、WPF→WPF 组、WEB_PORTAL→WEB_PORTAL 组，
//   即映射确定（同一入参始终得到同一结果）且正确，并据此定位到对应终端类型的默认菜单组。
// 测试框架：xUnit + FsCheck.Xunit（不自行实现属性测试框架），最少运行 100 次迭代。

using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;

namespace Ginkgo.Application.Tests.Properties;

/// <summary>
/// Property 13：clientType 映射确定且正确。
/// </summary>
public sealed class ClientTypeMappingProperty
{
    // Feature: multi-client-plugin-portal, Property 13: clientType 映射确定且正确
    /// <summary>
    /// 对任意合法外部入参（含大小写与首尾空白变体），<c>GetDefaultGroupIdAsync</c> 均按
    /// <c>MOBILE→UNIAPP</c>、<c>WPF→WPF</c>、<c>WEB_PORTAL→WEB_PORTAL</c> 归一化映射，
    /// 并定位到对应终端类型的 <c>IsDefault=1</c> 默认菜单组。
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void GetDefaultGroupId_Should_Map_ClientType_Deterministically_And_Correctly()
    {
        Prop.ForAll(ClientTypeInputGen().ToArbitrary(), testCase =>
        {
            var (input, expectedClientType) = testCase;

            // 每次迭代使用独立的内存库，避免跨迭代数据污染。
            using var db = new InMemoryTestDatabase();
            var groupRepo = new InMemoryRepository<MenuGroup>(db);

            // 为三个单一终端类型各预置一个默认菜单组（IsDefault=1）。
            var uniappGroup = MenuGroup.Create("默认移动端", "default-uniapp",
                clientType: PortalClientTypes.Uniapp, isSystem: true, isDefault: true);
            var webPortalGroup = MenuGroup.Create("默认WEB前台", "default-web-portal",
                clientType: PortalClientTypes.WebPortal, isSystem: true, isDefault: true);
            var wpfGroup = MenuGroup.Create("默认桌面端", "default-wpf",
                clientType: PortalClientTypes.Wpf, isSystem: true, isDefault: true);

            groupRepo.AddAsync(uniappGroup).GetAwaiter().GetResult();
            groupRepo.AddAsync(webPortalGroup).GetAwaiter().GetResult();
            groupRepo.AddAsync(wpfGroup).GetAwaiter().GetResult();

            var service = BuildService(db);

            // 期望命中的默认组：由「预期归一化终端类型」决定。
            var expectedGroupId = expectedClientType switch
            {
                PortalClientTypes.Uniapp => uniappGroup.Id,
                PortalClientTypes.WebPortal => webPortalGroup.Id,
                PortalClientTypes.Wpf => wpfGroup.Id,
                _ => 0L
            };

            var actualGroupId = service.GetDefaultGroupIdAsync(input).GetAwaiter().GetResult();

            // 断言：映射结果非空且恰好指向预期终端类型的默认组。
            return actualGroupId.HasValue && actualGroupId.Value == expectedGroupId;
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 生成覆盖三类合法外部入参及其大小写 / 首尾空白变体的 (入参, 预期归一化终端类型) 元组。
    /// </summary>
    private static Gen<(string Input, string Expected)> ClientTypeInputGen()
    {
        return Gen.Elements(
            // MOBILE → UNIAPP
            ("MOBILE", PortalClientTypes.Uniapp),
            ("mobile", PortalClientTypes.Uniapp),
            ("Mobile", PortalClientTypes.Uniapp),
            ("MoBiLe", PortalClientTypes.Uniapp),
            (" MOBILE ", PortalClientTypes.Uniapp),
            // WPF → WPF
            ("WPF", PortalClientTypes.Wpf),
            ("wpf", PortalClientTypes.Wpf),
            ("Wpf", PortalClientTypes.Wpf),
            (" wpf ", PortalClientTypes.Wpf),
            // WEB_PORTAL → WEB_PORTAL
            ("WEB_PORTAL", PortalClientTypes.WebPortal),
            ("web_portal", PortalClientTypes.WebPortal),
            ("Web_Portal", PortalClientTypes.WebPortal),
            (" WEB_PORTAL ", PortalClientTypes.WebPortal));
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
