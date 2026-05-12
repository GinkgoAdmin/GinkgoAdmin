// 文件功能说明：
// 内置执行提供器：调用已注册的 IInvocableAction。
// 运维人员在能力目录中选择一个动作，配置参数后定时执行。

using System.Text.Json;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Scheduling.Providers;

/// <summary>
/// 内置能力执行提供器 — 从 ActionRegistry 中查找并执行已注册的可调用动作。
/// </summary>
public sealed class ActionExecutionProvider : ITaskExecutionProvider
{
    private readonly ActionRegistry _actionRegistry;

    public ActionExecutionProvider(ActionRegistry actionRegistry)
    {
        _actionRegistry = actionRegistry;
    }

    public string SourceKey => "Action";
    public string DisplayName => "内置能力";
    public string? Icon => "bi-cpu";
    public string? Description => "选择一个已注册的内置能力（来自主框架或插件），配置参数后定时执行";
    public int Order => 10;

    public ExecutionFormDefinition GetFormDefinition()
    {
        return new ExecutionFormDefinition
        {
            Fields = new[]
            {
                new ExecutionFormField
                {
                    Name = "actionKey",
                    Label = "选择能力",
                    Type = "action-picker",
                    Required = true,
                    Description = "从已注册的能力目录中选择"
                }
                // 注意：选择能力后，前端需要动态追加该能力的参数表单
                // 参数统一存储在 ConfigJson.parameters 中
            }
        };
    }

    public Task<ExecutionValidationResult> ValidateAsync(string configJson, IServiceProvider services)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ActionConfig>(configJson, _jsonOpts);
            if (config == null || string.IsNullOrWhiteSpace(config.ActionKey))
                return Task.FromResult(ExecutionValidationResult.Fail("请选择要执行的能力"));

            var action = _actionRegistry.Get(config.ActionKey);
            if (action == null)
                return Task.FromResult(ExecutionValidationResult.Fail($"能力 [{config.ActionKey}] 未注册或已卸载"));

            return Task.FromResult(ExecutionValidationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExecutionValidationResult.Fail($"配置格式错误: {ex.Message}"));
        }
    }

    public async Task<ActionExecutionResult> ExecuteAsync(string configJson, ActionContext context)
    {
        var config = JsonSerializer.Deserialize<ActionConfig>(configJson, _jsonOpts)
            ?? throw new InvalidOperationException("ConfigJson 反序列化失败");

        var registered = _actionRegistry.Get(config.ActionKey)
            ?? throw new InvalidOperationException($"能力 [{config.ActionKey}] 未注册或已卸载");

        // 将配置中的参数传递给 ActionContext
        var parameters = config.Parameters ?? new Dictionary<string, object?>();
        var actionContext = new ActionContext(context.Services, context.CancellationToken, context.Logger, parameters);

        return await registered.Action.ExecuteAsync(actionContext);
    }

    private sealed class ActionConfig
    {
        public string ActionKey { get; set; } = string.Empty;
        public Dictionary<string, object?>? Parameters { get; set; }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
