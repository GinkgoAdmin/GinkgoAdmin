// 文件功能说明：
// 全局任务执行提供器注册表（Singleton）。
// 持有所有已注册的 ITaskExecutionProvider 实例，定时任务引擎按 SourceKey 查找对应提供器执行。

using System.Collections.Concurrent;
using Ginkgo.Plugin.Abstractions;

namespace Ginkgo.Infrastructure.Scheduling;

/// <summary>
/// 任务执行提供器注册表（Singleton 生命周期）。
/// 主框架注册 Action/Http/Sql，插件可注册 AIPrompt/Workflow 等自定义提供器。
/// </summary>
public sealed class ExecutionProviderRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册一个执行提供器。
    /// </summary>
    /// <param name="provider">提供器实例。</param>
    /// <param name="source">来源标识（BuiltIn 或模块Id）。</param>
    public void Register(ITaskExecutionProvider provider, string source = "BuiltIn")
    {
        _providers[provider.SourceKey] = new RegisteredProvider(provider, source);
    }

    /// <summary>
    /// 注销指定来源的所有提供器。
    /// </summary>
    public void UnregisterBySource(string source)
    {
        var keysToRemove = _providers
            .Where(kv => string.Equals(kv.Value.Source, source, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keysToRemove)
            _providers.TryRemove(key, out _);
    }

    /// <summary>
    /// 获取所有已注册提供器（按 Order 排序）。
    /// </summary>
    public IReadOnlyList<RegisteredProvider> GetAll()
        => _providers.Values.OrderBy(p => p.Provider.Order).ToList().AsReadOnly();

    /// <summary>
    /// 按 SourceKey 获取提供器。
    /// </summary>
    public ITaskExecutionProvider? Get(string sourceKey)
    {
        _providers.TryGetValue(sourceKey, out var result);
        return result?.Provider;
    }

    /// <summary>
    /// 已注册提供器信息。
    /// </summary>
    public sealed record RegisteredProvider(ITaskExecutionProvider Provider, string Source);
}
