// 文件功能说明：
// 定义实体变更拦截扩展点。模块实现此接口可在实体插入/更新时自动填充字段（如 TenantId）。

namespace Ginkgo.Plugin.Abstractions.Extensions;

/// <summary>
/// 实体变更拦截扩展点。
/// 框架在 Repository 执行插入/更新操作前，会收集所有已注册的 IEntityChangeInterceptor 并依次调用。
/// </summary>
/// <example>
/// 租户模块示例：
/// <code>
/// public class TenantEntityInterceptor : IEntityChangeInterceptor
/// {
///     public void OnInserting(object entity, IServiceProvider sp)
///     {
///         if (entity is TenantEntity te &amp;&amp; te.TenantId == null)
///         {
///             var tenant = sp.GetService&lt;ITenantContext&gt;();
///             te.TenantId = tenant?.CurrentTenantId;
///         }
///     }
///     public void OnUpdating(object entity, IServiceProvider sp) { }
/// }
/// </code>
/// </example>
public interface IEntityChangeInterceptor
{
    /// <summary>
    /// 实体插入前拦截，可用于自动填充字段。
    /// </summary>
    /// <param name="entity">即将插入的实体对象。</param>
    /// <param name="scopedServices">当前请求作用域的服务提供器。</param>
    void OnInserting(object entity, IServiceProvider scopedServices);

    /// <summary>
    /// 实体更新前拦截，可用于自动填充字段。
    /// </summary>
    /// <param name="entity">即将更新的实体对象。</param>
    /// <param name="scopedServices">当前请求作用域的服务提供器。</param>
    void OnUpdating(object entity, IServiceProvider scopedServices);

    /// <summary>
    /// 执行顺序（越小越先执行），默认 100。
    /// </summary>
    int Order => 100;
}
