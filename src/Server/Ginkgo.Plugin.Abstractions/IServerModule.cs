// 文件功能说明：
// 定义服务端插件模块的生命周期接口，包含初始化与加载/卸载钩子，用于模块化与热加载体系。

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 服务端模块接口，定义模块的注册与生命周期事件。
/// </summary>
public interface IServerModule
{
    /// <summary>
    /// 模块初始化，用于注册服务。
    /// </summary>
    /// <param name="services">依赖注入容器。</param>
    /// <param name="configuration">配置对象。</param>
    void Initialize(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// 模块加载回调（应用启动或模块热加载时触发）。
    /// </summary>
    /// <param name="services">服务提供器，用于解析已注册的服务。</param>
    Task OnLoadAsync(IServiceProvider services);

    /// <summary>
    /// 模块卸载回调（模块热卸载或应用停止时触发）。
    /// </summary>
    Task OnUnloadAsync();
}


