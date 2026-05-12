// 单例连接注册表：跨请求持久化通过 RegisterConnection 动态注册的多库连接配置。
// ISqlSugarClient（Scoped）工厂在创建每个新实例时从此注册表读取并批量附加。
// 与 TenantDbRouter 配合使用：TenantDbRouter 既写入此注册表，又附加到当前 Scoped 客户端。

using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 单例租户连接注册表，跨请求保存通过 <see cref="ITenantDbRouter.RegisterConnection"/> 动态注册的连接配置。
/// </summary>
public sealed class TenantConnectionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ConnectionConfig> _configs =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册或覆盖一条动态连接配置。</summary>
    public void Register(string configId, ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_gate) { _configs[configId] = config; }
    }

    /// <summary>移除一条动态连接配置。</summary>
    public bool Remove(string configId)
    {
        lock (_gate) { return _configs.Remove(configId); }
    }

    /// <summary>判断 ConfigId 是否已动态注册。</summary>
    public bool Exists(string configId)
    {
        lock (_gate) { return _configs.ContainsKey(configId); }
    }

    /// <summary>获取所有动态已注册的 ConfigId 列表。</summary>
    public IReadOnlyList<string> GetConfigIds()
    {
        lock (_gate) { return _configs.Keys.ToList(); }
    }

    /// <summary>
    /// 获取所有动态连接配置的快照，用于新建 ISqlSugarClient 时批量附加。
    /// </summary>
    public IReadOnlyList<ConnectionConfig> GetAllConfigs()
    {
        lock (_gate) { return _configs.Values.ToList(); }
    }
}
