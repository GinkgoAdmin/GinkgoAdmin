// 文件功能说明：
// 定义 JWT Claims 扩展点。模块实现此接口可在用户登录时向 JWT 令牌添加自定义 Claims。

using System.Security.Claims;

namespace Ginkgo.Plugin.Abstractions.Extensions;

/// <summary>
/// JWT Claims 扩展点。
/// 用户登录/刷新令牌时，框架会收集所有已注册的 IJwtClaimsContributor 并调用，将返回的 Claims 合并到 JWT 中。
/// </summary>
/// <example>
/// 租户模块示例：
/// <code>
/// public class TenantClaimsContributor : IJwtClaimsContributor
/// {
///     public async Task&lt;IEnumerable&lt;Claim&gt;&gt; GetAdditionalClaimsAsync(long userId, IServiceProvider sp)
///     {
///         var db = sp.GetRequiredService&lt;ISqlSugarClient&gt;();
///         var tenantId = await db.Ado.GetLongAsync("SELECT TenantId FROM ginkgo_Tenant_UserTenant WHERE UserId=@userId LIMIT 1", new { userId });
///         if (tenantId > 0) return new[] { new Claim("tenant_id", tenantId.ToString()) };
///         return Enumerable.Empty&lt;Claim&gt;();
///     }
/// }
/// </code>
/// </example>
public interface IJwtClaimsContributor
{
    /// <summary>
    /// 为指定用户生成额外的 JWT Claims。
    /// </summary>
    /// <param name="userId">当前登录用户的 Id。</param>
    /// <param name="scopedServices">当前请求作用域的服务提供器，可用于查询数据库等。</param>
    /// <returns>需要追加到 JWT 中的 Claims 集合。</returns>
    Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(long userId, IServiceProvider scopedServices);

    /// <summary>
    /// 执行顺序（越小越先执行），默认 100。
    /// </summary>
    int Order => 100;
}
