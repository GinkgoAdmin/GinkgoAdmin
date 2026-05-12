using Microsoft.Extensions.Hosting;

namespace Ginkgo.Api.Middlewares;

/// <summary>
/// 统一安全响应头中间件（PR-3 / P2）。
/// 为所有响应注入业界推荐的浏览器侧防御头，重点解决：
/// 1. <c>X-Content-Type-Options: nosniff</c> —— 禁止浏览器对响应做 MIME 嗅探，
///    防御"上传 JS 当图片"类的 XSS 投毒；
/// 2. <c>X-Frame-Options: DENY</c> —— 禁止页面被任意站点 iframe 嵌入，防点击劫持；
/// 3. <c>Referrer-Policy: strict-origin-when-cross-origin</c> —— 跨源仅传 origin，
///    避免后台 Token / 路径信息通过 Referer 泄漏；
/// 4. <c>Permissions-Policy</c> —— 默认禁用 camera / microphone / geolocation，
///    模块如需调用请显式覆盖；
/// 5. <c>Cross-Origin-Opener-Policy: same-origin</c> —— 避免被恶意页面 window.opener 串号；
/// 6. <c>Strict-Transport-Security</c>（仅生产 + HTTPS）—— 强制后续请求走 HTTPS；
/// 7. API 响应额外设置 <c>Cache-Control: no-store</c>，避免代理/浏览器缓存敏感 JSON。
///
/// 注意：CSP（Content-Security-Policy）不在通用中间件里下发，因为前端 SPA 的
/// 内联样式 / <c>unsafe-eval</c> 风险面与具体打包配置耦合，需要后续按页面单独评估再下发，
/// 此处只放安全且零业务侵入的头。
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isProduction;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _isProduction = env.IsProduction();
    }

    public Task InvokeAsync(HttpContext context)
    {
        // 在响应开始发送前注入头；用 OnStarting 兼容下游中间件主动写头的场景
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // 仅在缺失时写入，允许下游中间件/Controller 显式覆盖（例如 SSE 不要 no-store）
            if (!headers.ContainsKey("X-Content-Type-Options"))
                headers["X-Content-Type-Options"] = "nosniff";

            if (!headers.ContainsKey("X-Frame-Options"))
                headers["X-Frame-Options"] = "DENY";

            if (!headers.ContainsKey("Referrer-Policy"))
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            if (!headers.ContainsKey("Permissions-Policy"))
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            if (!headers.ContainsKey("Cross-Origin-Opener-Policy"))
                headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (_isProduction && context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
            {
                // max-age=180 天；includeSubDomains 让所有子域统一升级，避免主站 HTTPS 而 plugin-store 子域仍为 HTTP
                headers["Strict-Transport-Security"] = "max-age=15552000; includeSubDomains";
            }

            // API 响应禁缓存：路径以 /api 开头的视为 API；非 API（前端静态资源、SPA 入口）由 StaticFiles 配置自行设置缓存
            if (context.Request.Path.StartsWithSegments("/api")
                && !headers.ContainsKey("Cache-Control"))
            {
                headers["Cache-Control"] = "no-store";
                headers["Pragma"] = "no-cache";
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
