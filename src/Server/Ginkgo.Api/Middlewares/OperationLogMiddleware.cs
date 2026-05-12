using System.Text;
using System.Text.Json;
using Ginkgo.Domain.Logs;
using Ginkgo.Api.Services;

namespace Ginkgo.Api.Middlewares;

public sealed class OperationLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOperationLogQueue _logQueue;
    public OperationLogMiddleware(RequestDelegate next, IOperationLogQueue logQueue) { _next = next; _logQueue = logQueue; }

    public async Task Invoke(HttpContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string body = string.Empty;
        if (context.Request.Method != HttpMethods.Get && context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        string resBody = string.Empty;
        var p = context.Request.Path.ToString().ToLowerInvariant();
        bool shouldBufferResponse = !p.Contains("/stream") && !p.Contains("/download") && !p.Contains("/export");

        Stream? originalBodyStream = null;
        MemoryStream? memoryStream = null;

        if (shouldBufferResponse)
        {
            originalBodyStream = context.Response.Body;
            memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;
        }

        try
        {
            await _next(context);

            if (shouldBufferResponse && memoryStream != null && originalBodyStream != null)
            {
                memoryStream.Position = 0;
                // Limit response body size to ~2000 characters to prevent huge allocations
                using var resReader = new StreamReader(memoryStream, Encoding.UTF8, leaveOpen: true);
                var charBuffer = new char[2000];
                int charsRead = await resReader.ReadBlockAsync(charBuffer, 0, 2000);
                resBody = new string(charBuffer, 0, charsRead);
                
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);
            }

            var resStr = context.Response.StatusCode < 400 ? "OK" : $"HTTP{context.Response.StatusCode}";
            if (context.Items.TryGetValue("OpLogResult", out var customRes) && customRes is string cr)
            {
                resStr = cr;
            }
            // 尝试写日志，但不要因为触发器问题影响业务返回
            try { await TryWriteLog(context, sw.ElapsedMilliseconds, body, resBody, result: resStr); }
            catch { /* swallow */ }
        }
        catch (Exception ex)
        {
            if (shouldBufferResponse && originalBodyStream != null)
            {
                context.Response.Body = originalBodyStream;
            }
            try { await TryWriteLog(context, sw.ElapsedMilliseconds, body, string.Empty, result: $"EX:{ex.GetType().Name}", ex: ex); } catch { }
            throw;
        }
        finally
        {
            memoryStream?.Dispose();
        }
    }

    private Task TryWriteLog(HttpContext ctx, long elapsedMs, string body, string resBody, string result, Exception? ex = null)
    {
        // 只记录：查看单条/新增/修改/删除；分页列表仅在带筛选条件（如 keyword 等）时记录
        if (!ShouldLog(ctx)) return Task.CompletedTask;

        long? createdBy = null;
        var uid = ctx.User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) createdBy = gid;
        // 中文模块/功能映射（可扩展为配置/特性）
        (string moduleCN, string featureCN) = MapToChinese(ctx.Request.Path, ctx.Request.Method, ctx.Request.Query);

        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var ua = ctx.Request.Headers.UserAgent.ToString();
        string? dataJson = null;
        try
        {
            var payload = new Dictionary<string, object?>();
            // 写入异常追踪栈供安全审计
            if (ex != null)
            {
                payload["errorMessage"] = ex.Message;
                payload["stackTrace"] = ex.StackTrace;
            }

            // 提取自定义客户端头标记，精准溯源请求环境
            var clientType = ctx.Request.Headers["X-Client-Type"].ToString();
            if (!string.IsNullOrWhiteSpace(clientType)) payload["clientType"] = clientType;
            
            var platform = ctx.Request.Headers["X-Platform"].ToString();
            if (!string.IsNullOrWhiteSpace(platform)) payload["platform"] = platform;

            if (!string.IsNullOrWhiteSpace(ua)) payload["userAgent"] = ua;
            if (!string.IsNullOrWhiteSpace(body))
            {
                var safeBody = body;
                if (ctx.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
                {
                    safeBody = System.Text.RegularExpressions.Regex.Replace(safeBody, @"(password=)([^&]+)", "$1******", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    safeBody = System.Text.RegularExpressions.Regex.Replace(safeBody, @"""password""\s*:\s*""[^""]+""", @"""password"":""******""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    // 补充记录登录尝试失败时的试探用户名，方便被 AI 策略提取分析
                    var match = System.Text.RegularExpressions.Regex.Match(safeBody, @"(userName=)([^&]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!match.Success) 
                        match = System.Text.RegularExpressions.Regex.Match(safeBody, @"""userName""\s*:\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        payload["loginAttemptUser"] = match.Groups[match.Groups.Count - 1].Value;
                    }
                }
                payload["bodyText"] = safeBody;
            }
            if (!string.IsNullOrWhiteSpace(resBody)) payload["responseBody"] = resBody;

            dataJson = payload.Count > 0 ? JsonSerializer.Serialize(payload) : null;
        }
        catch { /* ignore data json build errors */ }

        var log = new OpLog
        {
            Action = ctx.Request.Method,
            Resource = ctx.Request.Path.ToString(),
            Ip = ip,
            UserAgent = ua,
            UserId = createdBy,
            Result = result,
            ElapsedMs = (int)Math.Min(int.MaxValue, Math.Max(0, elapsedMs)),
            DataJson = dataJson,
            ModuleCN = moduleCN,
            FeatureCN = featureCN,
            ReviewCN = $"{moduleCN}-{featureCN}-{(result.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? "成功" : "失败")}",
            At = DateTime.Now,  // 操作时间（数据库 NOT NULL）
            CreatedAt = DateTime.Now,
            CreatedBy = createdBy
        };
        // 附带 DepartmentId（若能从用户部门推导，则以首个部门填充；此处保持空，由 SQL 增量脚本回填）
        // 改为异步后台写入
        _logQueue.Enqueue(log);
        return Task.CompletedTask;
    }

    private static bool ShouldLog(HttpContext ctx)
    {
        var p = ctx.Request.Path.ToString().ToLowerInvariant();
        if (!p.StartsWith("/api/")) return false;
        
        // 排除日志和仪表盘等高频无特征接口，避免日志泛滥
        if (p.StartsWith("/api/v1/logs")) return false;
        if (p.StartsWith("/api/v1/dashboard")) return false;

        return true;
    }

    private static (string moduleCN, string featureCN) MapToChinese(PathString path, string method, IQueryCollection query)
    {
        var p = path.ToString().ToLowerInvariant();
        string module = "其他";
        if (p.Contains("/menus")) module = "菜单";
        else if (p.Contains("/users")) module = "用户";
        else if (p.Contains("/roles")) module = "角色";
        else if (p.Contains("/dictionaries")) module = "字典";
        else if (p.Contains("/auth")) module = "认证";
        else if (p.Contains("/logs")) module = "日志";

        string feature;
        var m = method.ToUpperInvariant();
        if (m == "GET")
        {
            var lastSeg = p.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            bool looksLikeGuid = Guid.TryParse(lastSeg, out _);
            if (looksLikeGuid) feature = "查看"; // 单条
            else
            {
                var hasFilter = query.Any(q =>
                    !string.Equals(q.Key, "page", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(q.Key, "pagesize", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(q.Value.ToString()));
                feature = hasFilter ? "搜索" : "查询";
            }
        }
        else if (m == "POST")
        {
            if (p.EndsWith("/auth/login")) feature = "登录";
            else feature = "新增";
        }
        else if (m == "PUT") feature = "修改";
        else if (m == "DELETE") 
        {
            if (p.EndsWith("/auth/logout")) feature = "登出";
            else feature = "删除";
        }
        else feature = method;
        return (module, feature);
    }
}


