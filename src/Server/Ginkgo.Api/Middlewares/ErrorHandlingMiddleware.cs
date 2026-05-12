// 文件功能说明：
// 全局异常处理中间件，统一捕获未处理异常并返回规范化结果。

using System.Net;
using Ginkgo.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Ginkgo.Api.Middlewares;

/// <summary>
/// 全局错误处理中间件。
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="next">后续中间件委托。</param>
    /// <param name="env">主机环境。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="configuration">配置。</param>
    public ErrorHandlingMiddleware(RequestDelegate next, IHostEnvironment env, ILogger<ErrorHandlingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _env = env;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 中间件处理入口。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            HttpStatusCode status = ex switch
            {
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                KeyNotFoundException => HttpStatusCode.NotFound,
                ArgumentException => HttpStatusCode.BadRequest,
                InvalidOperationException => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";

            var expose = _env.IsDevelopment() || string.Equals(_configuration["Debug:ExposeErrors"], "true", StringComparison.OrdinalIgnoreCase);
            if (expose)
            {
                var debugPayload = new { code = (int)status, message = ex.Message, stack = ex.ToString() };
                await context.Response.WriteAsJsonAsync(debugPayload);
            }
            else
            {
                var message = status switch
                {
                    HttpStatusCode.BadRequest => ex.Message,
                    HttpStatusCode.Unauthorized => "未授权",
                    HttpStatusCode.NotFound => "资源不存在",
                    _ => "服务器内部错误"
                };
                var result = Result.Fail((int)status, message);
                await context.Response.WriteAsJsonAsync(result);
            }
        }
    }
}


