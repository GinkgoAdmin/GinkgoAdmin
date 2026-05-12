// 文件功能说明：
// ITenantDbRouter 默认实现。Enabled=true 时通过 ISqlSugarClient.AsTenant().ChangeDatabase(configId) 执行真实切库。
// 启动期由 ApplySaasMultiDb 注入 db.json 静态连接，运行时通过 RegisterConnection 动态追加（由 Tenant 插件
// 在 OnLoadAsync 中从 ginkgo_Tenant_DbConnection 表读取并调用）。
// 注册为 Singleton 以保证连接状态跨请求一致。

using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// SaaS 多库路由器默认实现。
/// </summary>
public sealed class TenantDbRouter : ITenantDbRouter
{
    private readonly ISqlSugarClient _client;
    private readonly SaasMultiDbOptions _options;
    private readonly DbType _defaultDbType;
    private readonly ILogger<TenantDbRouter>? _logger;
    private readonly HashSet<string> _validConfigIds;
    private readonly object _gate = new();
    private string? _currentConfigId;

    public TenantDbRouter(
        ISqlSugarClient client,
        IOptions<DatabaseFeaturesOptions> features,
        IConfiguration configuration,
        ILogger<TenantDbRouter>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = features?.Value?.SaasMultiDb ?? new SaasMultiDbOptions();
        _defaultDbType = ResolveDefaultDbType(configuration);
        _logger = logger;
        _validConfigIds = BuildConfigIdSet(_options);
    }

    /// <summary>供单测注入自定义选项。</summary>
    internal TenantDbRouter(
        ISqlSugarClient client,
        SaasMultiDbOptions options,
        DbType defaultDbType = DbType.MySql,
        ILogger<TenantDbRouter>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new SaasMultiDbOptions();
        _defaultDbType = defaultDbType;
        _logger = logger;
        _validConfigIds = BuildConfigIdSet(_options);
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public string? CurrentConfigId => _currentConfigId;

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableConfigIds()
    {
        if (!_options.Enabled) return Array.Empty<string>();
        lock (_gate)
        {
            return _validConfigIds.ToList();
        }
    }

    /// <inheritdoc />
    public bool Exists(string configId)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(configId)) return false;
        lock (_gate) { return _validConfigIds.Contains(configId); }
    }

    /// <inheritdoc />
    public void ChangeDatabase(string configId)
    {
        EnsureEnabled();

        if (string.IsNullOrWhiteSpace(configId))
            throw new ArgumentException("configId 不能为空。", nameof(configId));

        lock (_gate)
        {
            if (!_validConfigIds.Contains(configId))
                throw new ArgumentException(
                    $"configId '{configId}' 未注册。当前可用：[{string.Join(", ", _validConfigIds)}]",
                    nameof(configId));
        }

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

        lock (_gate)
        {
            var tenant = _client.AsTenant();
            // 若已存在则先移除，等同"覆盖"。SqlSugar AsTenant 不暴露 RemoveConnection，
            // 但同 ConfigId 重新 AddConnection 在内部会被覆盖（基于 dict）。这里仍然刷新本地缓存集合。
            if (_validConfigIds.Contains(descriptor.ConfigId))
            {
                _logger?.LogDebug("[SaasMultiDb] ConfigId={ConfigId} 已存在，将覆盖更新。", descriptor.ConfigId);
            }

            tenant.AddConnection(new ConnectionConfig
            {
                ConfigId = descriptor.ConfigId,
                ConnectionString = descriptor.ConnectionString,
                DbType = dbType,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
            _validConfigIds.Add(descriptor.ConfigId);

            _logger?.LogInformation(
                "[SaasMultiDb] 动态注册租户库 ConfigId={ConfigId} (DbType={DbType}, Desc={Desc})",
                descriptor.ConfigId, dbType, descriptor.Description);
        }
    }

    /// <inheritdoc />
    public bool UnregisterConnection(string configId)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(configId)) return false;
        lock (_gate)
        {
            // SqlSugar ITenant 公开 API 没有 RemoveConnection；这里只把它从本地校验集合移除，
            // 后续 ChangeDatabase 会拒绝；旧的连接池在进程生命周期内残留，可接受。
            return _validConfigIds.Remove(configId);
        }
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
