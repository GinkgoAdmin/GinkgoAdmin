// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试的共享配置。
// 依据设计文档《Testing Strategy / 属性测试配置要求》：每条属性测试最少运行 100 次迭代。
// 提供统一的默认迭代次数常量，供各属性测试以 [Property(MaxTest = PortalPropertyConfig.MaxTest)] 引用，
// 避免在每个测试中重复硬编码迭代次数。

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 属性测试共享配置常量。
/// </summary>
public static class PortalPropertyConfig
{
    /// <summary>
    /// 默认属性测试迭代次数（最少 100 次）。
    /// 用法：[Property(MaxTest = PortalPropertyConfig.MaxTest)]。
    /// </summary>
    public const int MaxTest = 100;
}
