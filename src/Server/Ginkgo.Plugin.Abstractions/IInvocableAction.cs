// 文件功能说明：
// 定义可调用动作接口 — 能力的原子单元。
// 开发人员实现此接口注册一个可被定时任务、工作流、手动触发等方式调用的业务动作。
// 这是"开发一次，到处使用"的基石。

using Microsoft.Extensions.Logging;

namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 可调用动作（能力原子单元）。
/// 开发人员实现此接口，注册一个可被定时任务、工作流、手动触发等多种方式调用的业务动作。
/// </summary>
public interface IInvocableAction
{
    /// <summary>
    /// 动作唯一标识（建议格式：模块.功能，如 "CRM.SyncCustomerStatus"）。
    /// </summary>
    string ActionKey { get; }

    /// <summary>
    /// 显示名称（中文，用于后台管理界面展示）。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 分类（如 "系统维护"、"CRM"，用于界面分组展示）。
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 描述说明。
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 参数定义列表。前端根据此定义自动生成参数表单。
    /// 无参动作返回空数组即可。
    /// </summary>
    ActionParameterDefinition[] Parameters => Array.Empty<ActionParameterDefinition>();

    /// <summary>
    /// 执行动作。
    /// </summary>
    /// <param name="context">执行上下文，包含服务提供器、参数和取消令牌。</param>
    /// <returns>执行结果。</returns>
    Task<ActionExecutionResult> ExecuteAsync(ActionContext context);
}

/// <summary>
/// 动作执行上下文。
/// </summary>
public sealed class ActionContext
{
    /// <summary>
    /// 作用域级服务提供器（每次执行创建新 Scope）。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 取消令牌。
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 日志记录器。
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// 运维人员配置的参数（键值对）。
    /// </summary>
    public Dictionary<string, object?> Parameters { get; }

    public ActionContext(IServiceProvider services, CancellationToken cancellationToken, ILogger logger, Dictionary<string, object?>? parameters = null)
    {
        Services = services;
        CancellationToken = cancellationToken;
        Logger = logger;
        Parameters = parameters ?? new Dictionary<string, object?>();
    }
}

/// <summary>
/// 动作执行结果。
/// </summary>
public sealed class ActionExecutionResult
{
    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 结果消息（如 "同步完成，共处理 120 条客户"）。
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 附加数据（可被工作流引擎传递到下一步）。
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static ActionExecutionResult Ok(string? message = null, object? data = null)
        => new() { Success = true, Message = message, Data = data };

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static ActionExecutionResult Fail(string message, object? data = null)
        => new() { Success = false, Message = message, Data = data };
}

/// <summary>
/// 动作参数定义（描述一个参数的类型、约束和展示方式，前端据此自动生成表单控件）。
/// </summary>
public sealed class ActionParameterDefinition
{
    /// <summary>
    /// 参数键名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示标签。
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 控件类型：input / textarea / number / select / switch / datetime。
    /// </summary>
    public string Type { get; set; } = "input";

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 默认值。
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 占位提示文本。
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 参数说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 当 Type=select 时的选项列表。
    /// </summary>
    public ActionSelectOption[]? Options { get; set; }

    /// <summary>
    /// 当 Type=number 时的最小值。
    /// </summary>
    public int? MinValue { get; set; }

    /// <summary>
    /// 当 Type=number 时的最大值。
    /// </summary>
    public int? MaxValue { get; set; }
}

/// <summary>
/// 下拉选项。
/// </summary>
public sealed class ActionSelectOption
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public ActionSelectOption() { }
    public ActionSelectOption(string value, string label) { Value = value; Label = label; }
}
