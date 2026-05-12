using System.Text.Json;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Ginkgo.Api.Auth;

/// <summary>
/// 自定义授权结果处理：未登录/无权限时不返回 403/401，改为 200 + 业务结果提示。
/// </summary>
public sealed class FriendlyAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        // 检查端点是否标记了 [AllowAnonymous]，如果是则放行
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null)
        {
            await next(context);
            return;
        }

        // 检查 docs portal 路径 — 完全公开（权限由控制器内部按产品设置判断）
        var path = context.Request.Path.ToString();
        if (path.StartsWith("/api/docs/portal/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // DevScaffold 模块接口 — 登录即放行，Controller 内部由 IsDevelopmentMode() 控制
        if (path.StartsWith("/api/devscaffold/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // 根据鉴权结果返回 401 或 403 真实状态
        var isChallenged = authorizeResult.Challenged;
        context.Response.StatusCode = isChallenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        var message = isChallenged
            ? "未登录或登录已过期"
            : "无访问权限";

        var payload = Result.Fail(isChallenged ? 401 : 403, message); 
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}


