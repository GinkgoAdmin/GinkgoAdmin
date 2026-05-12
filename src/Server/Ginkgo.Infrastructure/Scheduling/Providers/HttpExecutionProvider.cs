// 文件功能说明：
// 内置执行提供器：发起 HTTP 请求。
// 运维人员配置 URL、Method、Headers、Body，定时调用内部或外部 API。

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Scheduling.Providers;

/// <summary>
/// HTTP 接口执行提供器 — 定时发起 HTTP 请求。
/// </summary>
public sealed class HttpExecutionProvider : ITaskExecutionProvider
{
    public string SourceKey => "Http";
    public string DisplayName => "HTTP 接口";
    public string? Icon => "bi-globe";
    public string? Description => "定时调用内部或外部 HTTP API（支持 GET/POST/PUT/DELETE）";
    public int Order => 20;
    public bool SupportsTest => true;

    public ExecutionFormDefinition GetFormDefinition()
    {
        return new ExecutionFormDefinition
        {
            Fields = new[]
            {
                new ExecutionFormField
                {
                    Name = "httpMethod",
                    Label = "请求方式",
                    Type = "select",
                    Required = true,
                    DefaultValue = "GET",
                    Options = new[]
                    {
                        new ActionSelectOption("GET", "GET"),
                        new ActionSelectOption("POST", "POST"),
                        new ActionSelectOption("PUT", "PUT"),
                        new ActionSelectOption("DELETE", "DELETE")
                    }
                },
                new ExecutionFormField
                {
                    Name = "httpUrl",
                    Label = "目标 URL",
                    Type = "input",
                    Required = true,
                    Placeholder = "https://example.com/api/xxx 或 http://localhost:5000/api/xxx"
                },
                new ExecutionFormField
                {
                    Name = "httpHeaders",
                    Label = "请求头 (JSON)",
                    Type = "json-editor",
                    Placeholder = "{\"Content-Type\": \"application/json\"}",
                    Rows = 3
                },
                new ExecutionFormField
                {
                    Name = "httpBody",
                    Label = "请求体",
                    Type = "code-editor",
                    DependsOn = "httpMethod:POST,PUT",
                    Rows = 5
                },
                new ExecutionFormField
                {
                    Name = "timeoutSeconds",
                    Label = "超时时间（秒）",
                    Type = "number",
                    DefaultValue = 30,
                    MinValue = 5,
                    MaxValue = 300
                },
                new ExecutionFormField
                {
                    Name = "retryCount",
                    Label = "失败重试次数",
                    Type = "number",
                    DefaultValue = 0,
                    MinValue = 0,
                    MaxValue = 5
                },
                new ExecutionFormField
                {
                    Name = "retryIntervalSeconds",
                    Label = "重试间隔（秒）",
                    Type = "number",
                    DefaultValue = 10,
                    MinValue = 1,
                    MaxValue = 300,
                    DependsOn = "retryCount:1,2,3,4,5"
                }
            }
        };
    }

    public Task<ExecutionValidationResult> ValidateAsync(string configJson, IServiceProvider services)
    {
        try
        {
            var config = JsonSerializer.Deserialize<HttpConfig>(configJson, _jsonOpts);
            if (config == null || string.IsNullOrWhiteSpace(config.HttpUrl))
                return Task.FromResult(ExecutionValidationResult.Fail("目标 URL 不能为空"));

            if (!Uri.TryCreate(config.HttpUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
                return Task.FromResult(ExecutionValidationResult.Fail("URL 格式不正确，必须以 http:// 或 https:// 开头"));

            return Task.FromResult(ExecutionValidationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExecutionValidationResult.Fail($"配置格式错误: {ex.Message}"));
        }
    }

    public async Task<ActionExecutionResult> ExecuteAsync(string configJson, ActionContext context)
    {
        return await DoExecuteAsync(configJson, context);
    }

    public async Task<ActionExecutionResult> TestAsync(string configJson, ActionContext context)
    {
        return await DoExecuteAsync(configJson, context);
    }

    private async Task<ActionExecutionResult> DoExecuteAsync(string configJson, ActionContext context)
    {
        var config = JsonSerializer.Deserialize<HttpConfig>(configJson, _jsonOpts)
            ?? throw new InvalidOperationException("ConfigJson 反序列化失败");

        var timeout = Math.Clamp(config.TimeoutSeconds ?? 30, 5, 300);
        var retryCount = Math.Clamp(config.RetryCount ?? 0, 0, 5);
        var retryInterval = Math.Clamp(config.RetryIntervalSeconds ?? 10, 1, 300);

        Exception? lastException = null;
        string? responseBody = null;
        int statusCode = 0;

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            if (attempt > 0)
            {
                context.Logger.LogInformation("HTTP 任务第 {Attempt} 次重试，等待 {Interval} 秒", attempt, retryInterval);
                await Task.Delay(TimeSpan.FromSeconds(retryInterval), context.CancellationToken);
            }

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) };
                var request = new HttpRequestMessage(ParseMethod(config.HttpMethod), config.HttpUrl);

                // 添加请求头
                if (config.HttpHeaders is { Count: > 0 })
                {
                    foreach (var (key, value) in config.HttpHeaders)
                        request.Headers.TryAddWithoutValidation(key, value);
                }

                // 添加请求体
                if (!string.IsNullOrWhiteSpace(config.HttpBody)
                    && (config.HttpMethod?.Equals("POST", StringComparison.OrdinalIgnoreCase) == true
                        || config.HttpMethod?.Equals("PUT", StringComparison.OrdinalIgnoreCase) == true))
                {
                    var contentType = config.HttpHeaders?.GetValueOrDefault("Content-Type") ?? "application/json";
                    request.Content = new StringContent(config.HttpBody, Encoding.UTF8, contentType);
                }

                var sw = Stopwatch.StartNew();
                var response = await httpClient.SendAsync(request, context.CancellationToken);
                sw.Stop();

                statusCode = (int)response.StatusCode;
                responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);

                // 截取响应前 500 字符用于日志
                var bodySummary = responseBody.Length > 500 ? responseBody[..500] + "..." : responseBody;

                if (response.IsSuccessStatusCode)
                {
                    return new ActionExecutionResult
                    {
                        Success = true,
                        Message = $"HTTP {statusCode} 成功 ({sw.ElapsedMilliseconds}ms)",
                        Data = new { statusCode, elapsed = sw.ElapsedMilliseconds, body = bodySummary }
                    };
                }
                else
                {
                    lastException = new HttpRequestException($"HTTP {statusCode}: {bodySummary}");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                context.Logger.LogWarning(ex, "HTTP 任务执行失败（第 {Attempt} 次）", attempt + 1);
            }
        }

        return ActionExecutionResult.Fail(
            $"HTTP 请求失败（重试 {retryCount} 次后）: {lastException?.Message}",
            new { statusCode, url = config.HttpUrl });
    }

    private static HttpMethod ParseMethod(string? method)
    {
        return method?.ToUpperInvariant() switch
        {
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => HttpMethod.Get
        };
    }

    private sealed class HttpConfig
    {
        public string? HttpMethod { get; set; }
        public string? HttpUrl { get; set; }
        public Dictionary<string, string>? HttpHeaders { get; set; }
        public string? HttpBody { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? RetryCount { get; set; }
        public int? RetryIntervalSeconds { get; set; }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
