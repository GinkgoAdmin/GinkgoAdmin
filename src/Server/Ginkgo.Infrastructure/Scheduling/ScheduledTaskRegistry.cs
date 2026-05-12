// 文件功能说明：
// 线程安全的定时任务注册表，持有所有已注册的 IScheduledTask 实例，供调度引擎查询。

using System.Collections.Concurrent;
using Ginkgo.Plugin.Abstractions;

namespace Ginkgo.Infrastructure.Scheduling;

/// <summary>
/// 定时任务注册表（Singleton 生命周期）。
/// 主框架内置任务在启动时注册，插件任务在模块 Load 时注册、Unload 时注销。
/// </summary>
public sealed class ScheduledTaskRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredTask> _tasks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册一个定时任务。
    /// </summary>
    /// <param name="task">任务实例。</param>
    /// <param name="source">来源标识（BuiltIn 或模块Id）。</param>
    /// <param name="sourceDisplayName">来源显示名称（如插件名称）。</param>
    /// <param name="groupName">分组名称（为空时使用任务自身分组）。</param>
    public void Register(IScheduledTask task, string source = "BuiltIn", string? sourceDisplayName = null, string? groupName = null)
    {
        _tasks[task.TaskKey] = new RegisteredTask(task, source, sourceDisplayName, groupName);
    }

    /// <summary>
    /// 注销指定来源的所有任务（模块卸载时调用）。
    /// </summary>
    /// <param name="source">来源标识。</param>
    public void UnregisterBySource(string source)
    {
        var keysToRemove = _tasks.Where(kv => string.Equals(kv.Value.Source, source, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keysToRemove)
        {
            _tasks.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 注销指定 TaskKey 的任务。
    /// </summary>
    public void Unregister(string taskKey)
    {
        _tasks.TryRemove(taskKey, out _);
    }

    /// <summary>
    /// 获取所有已注册任务。
    /// </summary>
    public IReadOnlyCollection<RegisteredTask> GetAll() => _tasks.Values.ToList().AsReadOnly();

    /// <summary>
    /// 按 TaskKey 获取任务。
    /// </summary>
    public RegisteredTask? Get(string taskKey)
    {
        _tasks.TryGetValue(taskKey, out var result);
        return result;
    }

    /// <summary>
    /// 已注册任务信息。
    /// </summary>
    public sealed record RegisteredTask(IScheduledTask Task, string Source, string? SourceDisplayName, string? GroupName);
}
