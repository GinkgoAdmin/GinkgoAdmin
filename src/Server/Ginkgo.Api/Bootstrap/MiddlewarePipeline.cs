using System;
using Ginkgo.Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Ginkgo.Api.Bootstrap;

public static class MiddlewarePipeline
{
    /// <summary>
    /// 统一的请求流水线（保持与 Program.cs 现有顺序一致）。
    /// 仅迁移请求级中间件：RequestId/Serilog/请求日志/错误处理/CORS/Auth/Authorization。
    /// 其他如静态文件、模块路由刷新、Swagger 保持在 Program.cs 中。
    /// </summary>
    public static IApplicationBuilder UseRequestPipeline(this IApplicationBuilder app, IConfiguration configuration, bool installationMode)
    {
        // 响应压缩（尽早注册，在其他中间件之前）
        app.UseResponseCompression();

        // RequestId（链路追踪）
        app.Use(async (ctx, next) =>
        {
            if (!ctx.Request.Headers.TryGetValue("X-Request-Id", out var rid) || string.IsNullOrWhiteSpace(rid))
            {
                rid = Ginkgo.Domain.Utils.SequentialGuid.NewGuid().ToString("N");
                ctx.Request.Headers["X-Request-Id"] = rid;
            }
            ctx.Response.Headers["X-Request-Id"] = rid;
            await next();
        });

        // Serilog 请求日志
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
        });

        // 统一请求/异常处理中间件
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();

        // P2：统一安全响应头（nosniff / X-Frame-Options / Referrer-Policy / HSTS / API no-store 等）
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // DB 操作日志根据配置与安装模式控制
        var enableDbOpLog = !string.Equals(configuration["Debug:DisableDbOpLog"], "true", StringComparison.OrdinalIgnoreCase);
        if (!installationMode && enableDbOpLog)
        {
            app.UseMiddleware<OperationLogMiddleware>();
        }

        app.UseCors("ConfiguredCors");
        app.UseRateLimiter();
        app.UseAuthentication();

        // 安装模式下未注册 Authorization 服务，这里跳过
        if (!installationMode)
        {
            app.UseAuthorization();
        }

        return app;
    }
}
