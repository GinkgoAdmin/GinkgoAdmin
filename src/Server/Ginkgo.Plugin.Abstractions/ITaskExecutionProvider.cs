// 文件功能说明：
// 定义任务执行提供器接口 — 可插拔的执行方式。
// 主框架提供 Action / Http / Sql 三种内置提供器，
// 插件可注册自己的提供器（如 AI 指令、工作流等），无需修改主框架。

namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 任务执行提供器。
/// 定义一种可由定时任务/工作流调用的执行方式。
/// 主框架和插件均可注册自己的提供器，实现可插拔执行方式扩展。
/// </summary>
public interface ITaskExecutionProvider
{
    /// <summary>
    /// 执行源标识（如 "Action"、"Http"、"Sql"、"AIPrompt"）。
    /// 存储在 ScheduledTaskRecord.ExecutionSource 中用于分发。
    /// </summary>
    string SourceKey { get; }

    /// <summary>
    /// 显示名称（如 "内置能力"、"HTTP 接口"、"AI 指令"）。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 图标标识（前端用，如 "bi-cpu"、"bi-globe"）。
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// 描述说明。
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 排序序号（决定在前端新增弹窗中的展示顺序，越小越靠前）。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 获取配置表单定义。前端根据此结构动态渲染配置表单，无需为每种类型写死 UI。
    /// </summary>
    ExecutionFormDefinition GetFormDefinition();

    /// <summary>
    /// 校验配置 JSON 是否合法。
    /// </summary>
    Task<ExecutionValidationResult> ValidateAsync(string configJson, IServiceProvider services);

    /// <summary>
    /// 执行任务。
    /// </summary>
    /// <param name="configJson">任务的完整执行配置 JSON。</param>
    /// <param name="context">执行上下文。</param>
    Task<ActionExecutionResult> ExecuteAsync(string configJson, ActionContext context);

    /// <summary>
    /// 是否支持测试执行（前端据此决定是否显示"测试"按钮）。
    /// </summary>
    bool SupportsTest => false;

    /// <summary>
    /// 测试执行（如 HTTP 的"测试连接"、SQL 的"试运行"）。
    /// </summary>
    Task<ActionExecutionResult> TestAsync(string configJson, ActionContext context)
        => Task.FromResult(ActionExecutionResult.Fail("此执行方式不支持测试"));
}

/// <summary>
/// 执行提供器的配置表单定义。
/// 前端根据此结构自动生成表单，不需要为每种执行方式编写独立 UI 组件。
/// </summary>
public sealed class ExecutionFormDefinition
{
    /// <summary>
    /// 表单字段列表。
    /// </summary>
    public ExecutionFormField[] Fields { get; set; } = Array.Empty<ExecutionFormField>();
}

/// <summary>
/// 配置表单中的一个字段定义。
/// </summary>
public sealed class ExecutionFormField
{
    /// <summary>
    /// 字段键名（对应 ConfigJson 中的属性名）。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示标签。
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 控件类型：input / textarea / number / select / switch / json-editor / code-editor / action-picker。
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
    /// 占位提示。
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 字段说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 下拉选项（Type=select 时使用）。
    /// </summary>
    public ActionSelectOption[]? Options { get; set; }

    /// <summary>
    /// 条件显示：依赖另一字段的值决定是否显示（格式："fieldName:value1,value2"）。
    /// </summary>
    public string? DependsOn { get; set; }

    /// <summary>
    /// 数值最小值。
    /// </summary>
    public int? MinValue { get; set; }

    /// <summary>
    /// 数值最大值。
    /// </summary>
    public int? MaxValue { get; set; }

    /// <summary>
    /// textarea / code-editor 行数。
    /// </summary>
    public int? Rows { get; set; }

    /// <summary>
    /// 是否支持多选（Type=select 时使用）。
    /// </summary>
    public bool Multiple { get; set; }
}

/// <summary>
/// 配置校验结果。
/// </summary>
public sealed class ExecutionValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static ExecutionValidationResult Ok() => new() { IsValid = true };
    public static ExecutionValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}
