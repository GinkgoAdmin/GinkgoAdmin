// 文件功能说明：
// 定义 SqlSugar 客户端配置扩展点。模块实现此接口可添加全局查询过滤器、AOP 等配置。

using SqlSugar;

namespace Ginkgo.Plugin.Abstractions.Extensions;

/// <summary>
/// SqlSugar 客户端配置扩展点。
/// 每个请求作用域创建 ISqlSugarClient 时，框架会收集所有已注册的 ISqlSugarConfigurator 并依次调用。
/// </summary>
/// <example>
/// 租户模块示例：
/// <code>
/// public class TenantSqlSugarConfigurator : ISqlSugarConfigurator
/// {
///     public void Configure(ISqlSugarClient client, IServiceProvider scopedServices)
///     {
///         var tenant = scopedServices.GetService&lt;ITenantContext&gt;();
///         if (tenant?.CurrentTenantId != null)
///             client.QueryFilter.AddTableFilter&lt;TenantEntity&gt;(it => it.TenantId == tenant.CurrentTenantId);
///     }
///     public int Order => 10;
/// }
/// </code>
/// </example>
public interface ISqlSugarConfigurator
{
    /// <summary>
    /// 配置 SqlSugar 客户端（每个请求作用域调用一次）。
    /// 可在此添加 QueryFilter、AOP 回调等。
    /// </summary>
    /// <param name="client">当前请求作用域的 SqlSugar 客户端实例。</param>
    /// <param name="scopedServices">当前请求作用域的服务提供器。</param>
    void Configure(ISqlSugarClient client, IServiceProvider scopedServices);

    /// <summary>
    /// 执行顺序（越小越先执行），默认 100。
    /// </summary>
    int Order => 100;
}
