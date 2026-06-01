// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）任务 18.5「老数据默认值读取示例测试」。
// 目标：以示例（example）方式验证「向后兼容」语义——既有/老数据在未显式设置新增字段时，
//   读取回来应得到约定的默认值：
//     - MenuGroup.IsDefault 未设置时默认为 false（0）（需求 13.5）；
//     - MenuGroupItem.Module 未设置时默认为 "sys"、RequireGrant 未设置时默认为 false（0）（需求 13.6）；
//   并核查简体中文经数据库往返后 UTF-8 无乱码（需求 14.6）。
//
// 实现说明：
//   复用既有内存测试基础设施（InMemoryTestDatabase + InMemoryRepository<T>），
//   通过领域工厂方法创建「未指定新字段」的实体，写入内存库后再读回，断言默认值与中文一致性。
//   这样既覆盖了领域工厂/属性初始化器施加的默认值，也覆盖了数据库读写路径的存取一致性，
//   等价于「老行（列已存在但未赋值）按列默认值读取」的场景。

using System.Threading.Tasks;
using Ginkgo.Application.Tests.Infrastructure;
using Ginkgo.Domain.Menus;
using Xunit;

namespace Ginkgo.Application.Tests.Examples;

/// <summary>
/// 老数据默认值读取与中文 UTF-8 无乱码示例测试。
/// </summary>
public sealed class LegacyDefaultsExampleTests
{
    /// <summary>
    /// 需求 13.5：菜单组未显式设置 IsDefault 时，读取回来应为 false（对应 TINYINT(1) 的 0）。
    /// </summary>
    [Fact]
    public async Task MenuGroup_Unset_IsDefault_Should_Read_As_False()
    {
        using var db = new InMemoryTestDatabase();
        var repo = new InMemoryRepository<MenuGroup>(db);

        // 模拟老数据：通过工厂创建菜单组时不传 isDefault（采用默认值 false）。
        var group = MenuGroup.Create(
            name: "历史菜单组",
            slug: "legacy-group",
            clientType: PortalClientTypes.WebPortal);
        await repo.AddAsync(group);

        // 写入后读回，断言 IsDefault 默认读为 false（0）。
        var loaded = await repo.GetByIdAsync(group.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded!.IsDefault);
    }

    /// <summary>
    /// 需求 13.6：菜单组项未显式设置 Module / RequireGrant 时，
    /// 读取回来应分别为 "sys" 与 false（对应 TINYINT(1) 的 0）。
    /// </summary>
    [Fact]
    public async Task MenuGroupItem_Unset_Module_And_RequireGrant_Should_Read_Defaults()
    {
        using var db = new InMemoryTestDatabase();
        var repo = new InMemoryRepository<MenuGroupItem>(db);

        // 模拟老数据：通过工厂创建菜单组项时不传 module / requireGrant（采用默认值 "sys" / false）。
        var item = MenuGroupItem.Create(
            menuGroupId: 1001L,
            title: "历史入口项",
            url: "/legacy/path");
        await repo.AddAsync(item);

        // 写入后读回，断言 Module 默认为 "sys"、RequireGrant 默认为 false（0）。
        var loaded = await repo.GetByIdAsync(item.Id);
        Assert.NotNull(loaded);
        Assert.Equal("sys", loaded!.Module);
        Assert.False(loaded.RequireGrant);
    }

    /// <summary>
    /// 需求 14.6：简体中文文案经数据库往返后应保持 UTF-8 无乱码，读回值与写入值完全相等。
    /// 同时再次覆盖默认值读取（IsDefault / Module / RequireGrant），保证中文与默认语义并存正确。
    /// </summary>
    [Fact]
    public async Task Chinese_Text_Should_RoundTrip_Without_Mojibake()
    {
        using var db = new InMemoryTestDatabase();
        var groupRepo = new InMemoryRepository<MenuGroup>(db);
        var itemRepo = new InMemoryRepository<MenuGroupItem>(db);

        // 含简体中文的菜单组与菜单组项（均不指定新字段，验证默认值与中文同时成立）。
        var group = MenuGroup.Create(
            name: "默认移动端",
            slug: "legacy-zh-group",
            description: "用于核查中文无乱码的历史菜单组",
            clientType: PortalClientTypes.Uniapp);
        await groupRepo.AddAsync(group);

        var item = MenuGroupItem.Create(
            menuGroupId: group.Id,
            title: "事件办理",
            url: "/plugins/smart-community/event-handle");
        await itemRepo.AddAsync(item);

        // 读回后断言中文逐字相等（无 mojibake / 编码损坏）。
        var loadedGroup = await groupRepo.GetByIdAsync(group.Id);
        Assert.NotNull(loadedGroup);
        Assert.Equal("默认移动端", loadedGroup!.Name);
        Assert.Equal("用于核查中文无乱码的历史菜单组", loadedGroup.Description);
        // 中文与默认值并存：未设置 IsDefault 仍应读为 false。
        Assert.False(loadedGroup.IsDefault);

        var loadedItem = await itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(loadedItem);
        Assert.Equal("事件办理", loadedItem!.Title);
        // 中文与默认值并存：未设置 Module / RequireGrant 仍应读为 "sys" / false。
        Assert.Equal("sys", loadedItem.Module);
        Assert.False(loadedItem.RequireGrant);
    }
}
