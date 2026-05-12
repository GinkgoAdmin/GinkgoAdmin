using System.Text;

namespace Ginkgo.Api.Middlewares;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IConfiguration _cfg;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IConfiguration cfg)
    {
        _next = next; _logger = logger; _cfg = cfg;
    }

    public async Task Invoke(HttpContext ctx)
    {
        var enabled = string.Equals(_cfg["Debug:EnableRequestLog"], "true", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(_cfg["Debug:Enable"], "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            await _next(ctx); return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var includeBody = string.Equals(_cfg["Debug:IncludeRequestBody"], "true", StringComparison.OrdinalIgnoreCase);
        var includeResp = string.Equals(_cfg["Debug:IncludeResponseBody"], "true", StringComparison.OrdinalIgnoreCase);
        var includeHeaders = string.Equals(_cfg["Debug:IncludeHeaders"], "true", StringComparison.OrdinalIgnoreCase);
        var maxBody = int.TryParse(_cfg["Debug:MaxBodySize"], out var n) ? Math.Max(0, n) : 2048;

        string? reqBody = null;
        if (includeBody && ctx.Request.Method != HttpMethods.Get && ctx.Request.ContentLength > 0 && ctx.Request.Body.CanSeek)
        {
            ctx.Request.EnableBuffering();
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var raw = await reader.ReadToEndAsync();
            ctx.Request.Body.Position = 0;
            reqBody = Truncate(raw, maxBody);
        }

        string? respBody = null;
        var original = ctx.Response.Body;
        using var mem = new MemoryStream();
        if (includeResp)
        {
            ctx.Response.Body = mem;
        }

        try
        {
            await _next(ctx);
        }
        finally
        {
            sw.Stop();
            if (includeResp)
            {
                mem.Position = 0;
                using var reader = new StreamReader(mem, Encoding.UTF8);
                var raw = await reader.ReadToEndAsync();
                respBody = Truncate(raw, maxBody);
                mem.Position = 0;
                await mem.CopyToAsync(original);
                ctx.Response.Body = original;
            }

            _logger.LogInformation("REQ {method} {path} {status} {ms}ms {headers} {req} {resp}",
                ctx.Request.Method,
                ctx.Request.Path + ctx.Request.QueryString,
                ctx.Response.StatusCode,
                sw.ElapsedMilliseconds,
                includeHeaders ? RedactHeaders(ctx.Request.Headers, _cfg["Debug:RedactHeaders"]) : null,
                reqBody,
                respBody);
        }
    }

    private static string Truncate(string s, int max) => s == null ? "" : (s.Length <= max ? s : (s[..max] + "..."));
    private static IDictionary<string, string> RedactHeaders(IHeaderDictionary headers, string? list)
    {
        var redact = (list ?? "Authorization,Cookie").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).ToHashSet();
        return headers.ToDictionary(k => k.Key, v => redact.Contains(v.Key.ToLowerInvariant()) ? "***" : v.Value.ToString());
    }
}




