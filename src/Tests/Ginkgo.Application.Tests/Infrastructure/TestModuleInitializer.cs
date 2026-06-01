// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试的程序集级初始化器。
// 雪花 Id 生成器（SnowflakeIdGenerator）是全局静态服务，领域实体在构造时即调用 NextId()，
// 因此测试进程必须在任何实体创建之前完成一次性初始化。使用 [ModuleInitializer] 保证在程序集
// 加载早期执行，且通过 IsInitialized 守卫避免与其他可能已初始化的场景重复初始化（重复会抛异常）。

using System.Runtime.CompilerServices;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 测试程序集模块初始化器。
/// </summary>
internal static class TestModuleInitializer
{
    /// <summary>
    /// 在程序集加载早期一次性初始化雪花 Id 生成器（使用固定机器 Id=1）。
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        if (!SnowflakeIdGenerator.IsInitialized)
        {
            SnowflakeIdGenerator.Initialize(machineId: 1);
        }
    }
}
