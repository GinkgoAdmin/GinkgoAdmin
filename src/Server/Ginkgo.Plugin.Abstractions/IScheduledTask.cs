// 文件功能说明：
// 定义定时任务接口与执行上下文，供主框架和插件模块注册可被调度系统管理的周期性任务。

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 定时任务接口。
/// 插件或主框架实现此接口以声明一个可被调度系统管理的周期性任务。
/// 概念上与 <see cref="Extensions.IEntityChangeInterceptor"/> 同级：
/// 都是"插件向框架声明自身某种能力"的约定接口，不含任何业务语义。
/// </summary>
public interface IScheduledTask
{
    /// <summary>
    /// 任务唯一标识（建议格式：模块.功能，如 System.VerificationCodeCleanup）。
    /// </summary>
    string TaskKey { get; }

    /// <summary>
    /// 任务显示名称（中文，用于后台管理界面展示）。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 任务分组（如"系统维护"、"激活码管理"等，用于后台界面分组筛选）。
    /// </summary>
    string Group { get; }

    /// <summary>
    /// 标准 5 段式 Cron 表达式（分 时 日 月 周）。
    /// 示例："0 3 * * *" 表示每天凌晨 3 点执行。
    /// </summary>
    string CronExpression { get; }

    /// <summary>
    /// 任务描述（可选，用于后台界面展示）。
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 执行类型（用于后台界面展示，如"内置方法"、"HTTP 回调"、"SQL 脚本"等）。
    /// 默认返回"内置方法"。
    /// </summary>
    string ExecutionType => "内置方法";

    /// <summary>
    /// 执行目标描述（如类全名、URL、SQL 片段等，帮助管理员了解任务具体做什么）。
    /// 默认返回实现类的完整类名。
    /// </summary>
    string ExecutionTarget => GetType().FullName ?? GetType().Name;

    /// <summary>
    /// 执行任务。
    /// </summary>
    /// <param name="context">执行上下文，包含服务提供器和取消令牌。</param>
    Task ExecuteAsync(ScheduledTaskContext context);
}

/// <summary>
/// 定时任务执行过程中的单条输出条目。
/// </summary>
public sealed class ScheduledTaskOutputEntry
{
    /// <summary>条目级别：Info / Warn / Error / Result。</summary>
    public string Level { get; set; } = "Info";

    /// <summary>记录时间（本地时间，UTC+8）。</summary>
    public DateTime At { get; set; } = DateTime.Now;

    /// <summary>正文内容。</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 定时任务执行上下文。
/// </summary>
public sealed class ScheduledTaskContext
{
    /// <summary>
    /// 作用域级服务提供器（每次执行创建新 Scope）。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 取消令牌（应用停止时触发）。
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 任务专属日志记录器。
    /// 注意：通过此 Logger 输出的 Info/Warn/Error 内容会被自动镜像到 <see cref="Output"/>，
    /// 最终序列化进 DetailsJson 在管理界面可见。
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// 任务执行过程的输出缓冲（线程安全）。
    /// task 主动调用 <see cref="WriteOutput"/>、<see cref="WriteWarn"/>、<see cref="WriteResult"/>
    /// 即可写入；调度器在执行结束时会序列化为 ScheduledTaskLog.DetailsJson。
    /// </summary>
    public IList<ScheduledTaskOutputEntry> Output { get; } = new List<ScheduledTaskOutputEntry>();

    public ScheduledTaskContext(IServiceProvider services, CancellationToken cancellationToken, ILogger logger)
    {
        Services = services;
        CancellationToken = cancellationToken;
        Logger = logger;
    }

    /// <summary>记录一条普通输出（同时镜像到 Logger.LogInformation）。</summary>
    public void WriteOutput(string message)
    {
        Append("Info", message);
        Logger.LogInformation("{Message}", message);
    }

    /// <summary>记录一条警告输出（同时镜像到 Logger.LogWarning）。</summary>
    public void WriteWarn(string message)
    {
        Append("Warn", message);
        Logger.LogWarning("{Message}", message);
    }

    /// <summary>记录任务结果摘要（用于在管理界面突出显示，如"完成：新增 X 条"）。</summary>
    public void WriteResult(string message)
    {
        Append("Result", message);
        Logger.LogInformation("[结果] {Message}", message);
    }

    /// <summary>仅记录到输出缓冲（不写 Logger），适用于已经写过日志只想补一份给管理界面的场景。</summary>
    public void AppendOnly(string level, string message) => Append(level, message);

    private void Append(string level, string message)
    {
        lock (Output)
        {
            Output.Add(new ScheduledTaskOutputEntry
            {
                Level = level,
                At = DateTime.Now,
                Message = message ?? string.Empty
            });
        }
    }
}
