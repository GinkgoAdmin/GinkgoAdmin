using Ginkgo.Domain.Tenant;

namespace Ginkgo.Api.Middlewares;

/// <summary>
/// 租户解析中间件：从 JWT Claims / Header / 子域名解析 TenantId。
/// 需在 UseAuthentication 之后注册。
/// </summary>
public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // 优先级 1：JWT Claims 中的 tenant_id
        var tenantClaim = context.User?.Claims
            .FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tid")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim) && long.TryParse(tenantClaim, out var tid1))
        {
            tenantContext.CurrentTenantId = tid1;
        }
        // 优先级 2：请求头 X-Tenant-Id
        else if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTid)
                 && long.TryParse(headerTid.FirstOrDefault(), out var tid2))
        {
            tenantContext.CurrentTenantId = tid2;
        }
        // 优先级 3：子域名（如 tenant1.example.com）
        else
        {
            var host = context.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length >= 3)
            {
                // 子域名作为租户标识（需要额外的租户名→ID映射，此处留扩展点）
                // tenantContext.CurrentTenantId = await ResolveTenantBySubdomain(parts[0]);
            }
        }

        await _next(context);
    }
}
