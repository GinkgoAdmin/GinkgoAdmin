// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）的示例测试（SMOKE/EXAMPLE）。
// 对应 tasks.md 任务 18.3：install.json ClientMenus 解析与两插件配置值示例测试。
// 通过真实解析器 Ginkgo.Api.Modules.ModuleSqlExecutor.ReadInstallJson 解析两个示例插件
// （smart-community、evaluate）真实的 install.json，断言 ClientMenus 段解析、字段完整性，
// 以及各入口项的 requireGrant / path 等配置值，从而验证任务 16.1 / 16.3 已写入正确配置。
// 覆盖需求：5.1、5.3、11.1、11.2、11.3、12.1、12.2、12.3。

using System;
using System.IO;
using System.Linq;
using Ginkgo.Api.Modules;
using Xunit;

namespace Ginkgo.Application.Tests.Examples;

/// <summary>
/// install.json 的 ClientMenus 段解析与两示例插件配置值的示例测试。
/// 直接调用主框架真实解析器 <see cref="ModuleSqlExecutor.ReadInstallJson(string)"/>，
/// 对仓库内真实的 install.json 文件做断言。
/// </summary>
public sealed class ClientMenusInstallSpecExampleTests
{
    // 两个示例插件 install.json 相对仓库根目录的路径（src/Module/...）。
    private const string SmartCommunityRelative =
        "src/Module/Ginkgo.Module.SmartCommunity/server/install.json";
    private const string EvaluateRelative =
        "src/Module/Ginkgo.Module.Evaluate/server/install.json";

    /// <summary>
    /// smart-community install.json 解析：
    /// 断言 ClientMenus 段存在、含 UNIAPP 声明（需求 5.1、11.1）；
    /// 「事件办理」入口 requireGrant=true、path 正确（需求 11.2、5.3）；
    /// 「智慧社区」居民入口 requireGrant=false（需求 11.3）；
    /// 且每个入口项 Title/Path 非空并具备 Order（字段完整性，需求 5.3）。
    /// </summary>
    [Fact]
    public void SmartCommunity_ClientMenus_Should_Parse_With_Expected_Config_Values()
    {
        var path = ResolveInstallJsonPath(SmartCommunityRelative);
        var spec = ModuleSqlExecutor.ReadInstallJson(path);

        Assert.NotNull(spec);

        // 需求 5.1：ClientMenus 段可被解析且独立于 Menus 段
        Assert.NotNull(spec!.ClientMenus);
        Assert.NotEmpty(spec.ClientMenus!);
        // 既有 Menus 段仍正常解析，互不影响
        Assert.NotNull(spec.Menus);

        // 需求 11.1：声明 clientType=UNIAPP 的业务入口
        var uniapp = spec.ClientMenus!
            .FirstOrDefault(c => string.Equals(c.ClientType, "UNIAPP", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(uniapp);
        Assert.NotNull(uniapp!.Items);
        Assert.NotEmpty(uniapp.Items!);

        // 字段完整性（需求 5.3）：每个入口项 Title / Path 非空，且具备 Order
        foreach (var item in uniapp.Items!)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Title), "入口项 Title 不应为空");
            Assert.False(string.IsNullOrWhiteSpace(item.Path), "入口项 Path 不应为空");
            Assert.True(item.Order >= 0, "入口项 Order 应为非负序号");
        }

        // 需求 11.2、5.3：「事件办理」入口 requireGrant=true 且 path 正确
        var eventHandle = uniapp.Items!.FirstOrDefault(i => i.Title == "事件办理");
        Assert.NotNull(eventHandle);
        Assert.True(eventHandle!.RequireGrant, "「事件办理」应声明为需要授权（requireGrant=true）");
        Assert.Equal("/pages/plugins/smart-community/event-handle", eventHandle.Path);

        // 需求 11.3：居民功能入口「智慧社区」requireGrant=false（对所有登录用户可见）
        var resident = uniapp.Items!.FirstOrDefault(i => i.Title == "智慧社区");
        Assert.NotNull(resident);
        Assert.False(resident!.RequireGrant, "居民功能入口应声明为无需授权（requireGrant=false）");
        Assert.Equal("/pages/plugins/smart-community/index", resident.Path);
    }

    /// <summary>
    /// evaluate install.json 解析：
    /// 断言 ClientMenus 段含 UNIAPP 声明（需求 12.1）；
    /// 「评估中心」入口 requireGrant=true 且 path 正确（需求 12.2、12.3）。
    /// </summary>
    [Fact]
    public void Evaluate_ClientMenus_Should_Parse_With_Expected_Config_Values()
    {
        var path = ResolveInstallJsonPath(EvaluateRelative);
        var spec = ModuleSqlExecutor.ReadInstallJson(path);

        Assert.NotNull(spec);

        // 需求 12.1：声明 clientType=UNIAPP 的「评估中心」入口
        Assert.NotNull(spec!.ClientMenus);
        var uniapp = spec.ClientMenus!
            .FirstOrDefault(c => string.Equals(c.ClientType, "UNIAPP", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(uniapp);
        Assert.NotNull(uniapp!.Items);
        Assert.NotEmpty(uniapp.Items!);

        // 字段完整性（需求 5.3）：每个入口项 Title / Path 非空，且具备 Order
        foreach (var item in uniapp.Items!)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Title), "入口项 Title 不应为空");
            Assert.False(string.IsNullOrWhiteSpace(item.Path), "入口项 Path 不应为空");
            Assert.True(item.Order >= 0, "入口项 Order 应为非负序号");
        }

        // 需求 12.2、12.3：「评估中心」入口 requireGrant=true 且 path 正确
        var evaluateCenter = uniapp.Items!.FirstOrDefault(i => i.Title == "评估中心");
        Assert.NotNull(evaluateCenter);
        Assert.True(evaluateCenter!.RequireGrant, "「评估中心」应声明为需要授权（requireGrant=true）");
        Assert.Equal("/pages/plugins/evaluate/index", evaluateCenter.Path);
    }

    /// <summary>
    /// 从测试运行目录（bin）向上逐级查找仓库根目录，定位真实的 install.json 文件。
    /// 仓库根目录约定为「包含 src/Module 的目录」。测试运行时位于测试工程 bin 目录下，
    /// 因此需要向上回溯若干层才能命中仓库根。若任意候选目录下存在目标 install.json 则返回其绝对路径。
    /// 该策略与 ModuleInstaller 向上查找插件目录的方式一致，确保校验的是仓库内真实文件，
    /// 从而验证任务 16.1 / 16.3 已写入正确配置。
    /// </summary>
    private static string ResolveInstallJsonPath(string relativePath)
    {
        // 同时从测试基目录与当前工作目录向上回溯，提升不同运行环境下的健壮性。
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
        }

        // 未能在运行环境中定位真实文件时，给出清晰的失败信息（不静默回退到伪造内容，
        // 以保证本示例测试始终校验的是仓库内真实 install.json）。
        throw new FileNotFoundException(
            $"未能从测试运行目录向上定位真实的 install.json：{relativePath}。" +
            $"起始目录：BaseDirectory={AppContext.BaseDirectory}，CurrentDirectory={Directory.GetCurrentDirectory()}。");
    }
}
