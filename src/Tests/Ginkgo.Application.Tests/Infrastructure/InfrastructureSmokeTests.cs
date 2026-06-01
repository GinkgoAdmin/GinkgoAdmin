// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试基础设施的冒烟测试。
// 仅用于验证内存仓储、生成器与共享配置可正常工作（建表 / 读写 / 软删除过滤 / 生成器产出），
// 不属于设计文档列出的 16 条编号正确性属性（那些在后续任务中逐条实现）。

using System.Linq;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Ginkgo.Domain.Menus;
using Xunit;

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 基础设施冒烟测试。
/// </summary>
public sealed class InfrastructureSmokeTests
{
    /// <summary>
    /// 内存仓储应能完成菜单组的写入与查询，并对软删除生效。
    /// </summary>
    [Fact]
    public async Task InMemoryRepository_Should_Insert_Query_And_SoftDelete()
    {
        using var db = new InMemoryTestDatabase();
        var repo = new InMemoryRepository<MenuGroup>(db);

        var group = MenuGroup.Create("默认移动端", "default-uniapp", clientType: PortalClientTypes.Uniapp,
            isSystem: true, isDefault: true);
        await repo.AddAsync(group);

        // 写入后可查询到
        var loaded = await repo.GetByIdAsync(group.Id);
        Assert.NotNull(loaded);
        Assert.True(loaded!.IsDefault);
        Assert.Equal(PortalClientTypes.Uniapp, loaded.ClientType);

        // Query() LINQ 可用
        var viaQuery = repo.Query().Where(x => x.IsDefault && x.ClientType == PortalClientTypes.Uniapp).ToList();
        Assert.Single(viaQuery);

        // 软删除后不可见
        await repo.DeleteAsync(group.Id);
        Assert.Null(await repo.GetByIdAsync(group.Id));
        Assert.Empty(repo.Query().ToList());
    }

    /// <summary>
    /// 不同 InMemoryTestDatabase 实例之间数据相互隔离（模拟按库隔离的租户上下文）。
    /// </summary>
    [Fact]
    public async Task InMemoryDatabases_Should_Be_Isolated()
    {
        using var db1 = new InMemoryTestDatabase();
        using var db2 = new InMemoryTestDatabase();
        var repo1 = new InMemoryRepository<MenuGroupItem>(db1);
        var repo2 = new InMemoryRepository<MenuGroupItem>(db2);

        var item = MenuGroupItem.Create(menuGroupId: 1, title: "事件办理", url: "/a",
            module: "Ginkgo.Module.Demo", requireGrant: true);
        await repo1.AddAsync(item);

        Assert.Single(repo1.Query().ToList());
        Assert.Empty(repo2.Query().ToList());
    }

    /// <summary>
    /// 共享配置的默认迭代次数应至少为 100，满足设计要求。
    /// </summary>
    [Fact]
    public void PropertyConfig_MaxTest_Should_Be_At_Least_100()
    {
        Assert.True(PortalPropertyConfig.MaxTest >= 100);
    }

    /// <summary>
    /// 菜单组项生成器产出的项均隶属指定菜单组（覆盖混合 Module、RequireGrant、Order、父子/孤儿）。
    /// 以最少 100 次迭代验证生成器可稳定产出可用数据。
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void Generators_Should_Produce_Items_For_Given_Group()
    {
        const long groupId = 12345L;
        Prop.ForAll(PortalGenerators.MenuGroupItemListGen(groupId).ToArbitrary(), items =>
        {
            // 所有生成项归属同一菜单组
            return items.All(x => x.MenuGroupId == groupId);
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// 客户端入口声明生成器应能产出空集合与含重复 path 的集合（覆盖边界）。
    /// </summary>
    [Property(MaxTest = PortalPropertyConfig.MaxTest)]
    public void ClientMenuItemSpec_Generator_Should_Produce_Valid_Specs()
    {
        Prop.ForAll(PortalGenerators.ClientMenuItemSpecListGen().ToArbitrary(), specs =>
        {
            // 每个声明项的 path 与 title 非空
            return specs.All(s => !string.IsNullOrEmpty(s.Path) && !string.IsNullOrEmpty(s.Title));
        }).QuickCheckThrowOnFailure();
    }
}
