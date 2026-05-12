// 文件功能说明：
// 全局可调用动作注册表（Singleton）。
// 持有所有已注册的 IInvocableAction 实例，供定时任务、工作流、手动触发等消费者查询和执行。

using System.Collections.Concurrent;
using Ginkgo.Plugin.Abstractions;

namespace Ginkgo.Infrastructure.Scheduling;

/// <summary>
/// 可调用动作注册表（Singleton 生命周期）。
/// 主框架内置动作在启动时注册，插件动作在模块 Load 时注册、Unload 时注销。
/// </summary>
public sealed class ActionRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredAction> _actions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册一个可调用动作。
    /// </summary>
    /// <param name="action">动作实例。</param>
    /// <param name="source">来源标识（BuiltIn 或模块Id）。</param>
    /// <param name="sourceDisplayName">来源显示名称（如插件名称）。</param>
    public void Register(IInvocableAction action, string source = "BuiltIn", string? sourceDisplayName = null)
    {
        _actions[action.ActionKey] = new RegisteredAction(action, source, sourceDisplayName);
    }

    /// <summary>
    /// 注销指定来源的所有动作。
    /// </summary>
    public void UnregisterBySource(string source)
    {
        var keysToRemove = _actions
            .Where(kv => string.Equals(kv.Value.Source, source, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keysToRemove)
            _actions.TryRemove(key, out _);
    }

    /// <summary>
    /// 注销指定 ActionKey 的动作。
    /// </summary>
    public void Unregister(string actionKey)
    {
        _actions.TryRemove(actionKey, out _);
    }

    /// <summary>
    /// 获取所有已注册动作。
    /// </summary>
    public IReadOnlyCollection<RegisteredAction> GetAll() => _actions.Values.ToList().AsReadOnly();

    /// <summary>
    /// 按 ActionKey 获取动作。
    /// </summary>
    public RegisteredAction? Get(string actionKey)
    {
        _actions.TryGetValue(actionKey, out var result);
        return result;
    }

    /// <summary>
    /// 已注册动作信息。
    /// </summary>
    public sealed record RegisteredAction(IInvocableAction Action, string Source, string? SourceDisplayName);
}
