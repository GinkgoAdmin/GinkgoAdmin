// 文件功能说明：
// ITenantDbRouter 默认实现。Enabled=true 时通过 ISqlSugarClient.AsTenant().ChangeDatabase(configId) 执行真实切库。
// 启动期由 ApplySaasMultiDb 注入 db.json 静态连接，运行时通过 RegisterConnection 动态追加（由 Tenant 插件
// 在 OnLoadAsync 中从 ginkgo_Tenant_DbConnection 表读取并调用）。
//
// 生命周期设计：
// - TenantDbRouter 注册为 Scoped（每请求一个实例），可安全注入 Scoped 的 ISqlSugarClient。
// - 动态连接配置（RegisterConnection 注入的）持久化在单例 TenantConnectionStore 中，
//   每次新建 Scoped ISqlSugarClient 时工厂会从 Store 读取并批量附加，确保跨请求可用。
// - 静态连接（db.json SaasMultiDb.Connections）由 ApplySaasMultiDb 在 ISqlSugarClient 工厂中统一注册。

using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// SaaS 多库路由器默认实现（Scoped）。
/// </summary>
public sealed class TenantDbRouter : ITenantDbRouter
{
    private readonly ISqlSugarClient _client;
    private readonly SaasMultiDbOptions _options;
    private readonly DbType _defaultDbType;
    private readonly ILogger<TenantDbRouter>? _logger;
    private readonly TenantConnectionStore? _store;
    private readonly HashSet<string> _staticConfigIds;
    private string? _currentConfigId;

    public TenantDbRouter(
        ISqlSugarClient client,
        IOptions<DatabaseFeaturesOptions> features,
        IConfiguration configuration,
        TenantConnectionStore store,
        ILogger<TenantDbRouter>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = features?.Value?.SaasMultiDb ?? new SaasMultiDbOptions();
        _defaultDbType = ResolveDefaultDbType(configuration);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
        _staticConfigIds = BuildConfigIdSet(_options);
    }

    /// <summary>供单测注入自定义选项（不依赖 TenantConnectionStore）。</summary>
    internal TenantDbRouter(
        ISqlSugarClient client,
        SaasMultiDbOptions options,
        DbType defaultDbType = DbType.MySql,
        ILogger<TenantDbRouter>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new SaasMultiDbOptions();
        _defaultDbType = defaultDbType;
        _store = null;
        _logger = logger;
        _staticConfigIds = BuildConfigIdSet(_options);
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public string? CurrentConfigId => _currentConfigId;

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableConfigIds()
    {
        if (!_options.Enabled) return Array.Empty<string>();
        var ids = new HashSet<string>(_staticConfigIds, StringComparer.OrdinalIgnoreCase);
        if (_store != null)
        {
            foreach (var id in _store.GetConfigIds()) ids.Add(id);
        }
        return ids.ToList();
    }

    /// <inheritdoc />
    public bool Exists(string configId)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(configId)) return false;
        return _staticConfigIds.Contains(configId) || (_store?.Exists(configId) ?? false);
    }

    /// <inheritdoc />
    public void ChangeDatabase(string configId)
    {
        EnsureEnabled();

        if (string.IsNullOrWhiteSpace(configId))
            throw new ArgumentException("configId 不能为空。", nameof(configId));

        if (!Exists(configId))
            throw new ArgumentException(
                $"configId '{configId}' 未注册。当前可用：[{string.Join(", ", GetAvailableConfigIds())}]",
                nameof(configId));

        _client.AsTenant().ChangeDatabase(configId);
        _currentConfigId = configId;

        _logger?.LogDebug("[SaasMultiDb] 已切换到 ConfigId={ConfigId}", configId);
    }

    /// <inheritdoc />
    public void RegisterConnection(TenantDbConnectionDescriptor descriptor)
    {
        EnsureEnabled();
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.ConfigId))
            throw new ArgumentException("ConfigId 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.ConnectionString))
            throw new ArgumentException("ConnectionString 不能为空。", nameof(descriptor));

        var dbType = ParseDbType(descriptor.DbType) ?? _defaultDbType;

        var connConfig = new ConnectionConfig
        {
            ConfigId = descriptor.ConfigId,
            ConnectionString = descriptor.ConnectionString,
            DbType = dbType,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        };

        // 持久化到跨请求 Store：下次新建 Scoped ISqlSugarClient 时工厂会自动附加
        _store?.Register(descriptor.ConfigId, connConfig);

        // 同步附加到当前 Scoped 请求的 SqlSugar 客户端
        _client.AsTenant().AddConnection(connConfig);

        _logger?.LogInformation(
            "[SaasMultiDb] 动态注册租户库 ConfigId={ConfigId} (DbType={DbType}, Desc={Desc})",
            descriptor.ConfigId, dbType, descriptor.Description);
    }

    /// <inheritdoc />
    public bool UnregisterConnection(string configId)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(configId)) return false;
        // SqlSugar ITenant 公开 API 没有 RemoveConnection；仅从 Store 移除，后续 ChangeDatabase 会拒绝。
        return _store?.Remove(configId) ?? false;
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
        {
            throw new NotSupportedException(
                "SaaS 多库路由未启用。请在 db.json 设置 Database.Features.SaasMultiDb.Enabled = true。");
        }
    }

    private static HashSet<string> BuildConfigIdSet(SaasMultiDbOptions options)
    {
        if (options?.Connections == null || options.Connections.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(
            options.Connections
                .Where(c => !string.IsNullOrWhiteSpace(c.ConfigId))
                .Select(c => c.ConfigId),
            StringComparer.OrdinalIgnoreCase);
    }

    private static DbType ResolveDefaultDbType(IConfiguration? configuration)
    {
        var name = configuration?["Database:Provider"];
        return ParseDbType(name) ?? DbType.MySql;
    }

    private static DbType? ParseDbType(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return name.Trim().ToLowerInvariant() switch
        {
            "mysql" => DbType.MySql,
            "sqlserver" or "mssql" => DbType.SqlServer,
            "postgresql" or "postgres" or "pgsql" => DbType.PostgreSQL,
            "sqlite" => DbType.Sqlite,
            "oracle" => DbType.Oracle,
            _ => null
        };
    }
}
