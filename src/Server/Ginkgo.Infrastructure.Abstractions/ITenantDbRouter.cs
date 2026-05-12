// 文件功能说明：
// SaaS 多库路由器抽象。当 Database.Features.SaasMultiDb.Enabled=true 时，
// 框架在 SqlSugar 上注册多个 ConfigId 连接（来自 db.json 静态配置 + 主库 ginkgo_Tenant_DbConnection 表动态注入），
// 业务侧通过 ITenantDbRouter.ChangeDatabase(configId) 切换到目标租户库。
//
// 设计要点：
// - Enabled=false 时 ChangeDatabase 抛 NotSupportedException；IsEnabled=false；
//   GetAvailableConfigIds 返回空列表。系统保持单库模式。
// - Enabled=true 时 ChangeDatabase 委托 SqlSugar ITenant.ChangeDatabase(configId)；
//   调用后当前请求 scope 内的后续 DB 操作全部路由到目标库。
// - 启动期通过 db.json Connections 注册的连接 + 运行时通过 RegisterConnection 动态注册的连接，二者并存且 ConfigId 唯一。
// - 不影响现有的 TenantId 列级过滤（TenantSqlSugarConfigurator），两者可叠加使用。
//
// 线程安全：实现侧对动态注入需要加锁，避免并发请求场景下重复 AddConnection。

namespace Ginkgo.Infrastructure.Abstractions;

/// <summary>
/// 单个租户库连接的描述。供 <see cref="ITenantDbRouter.RegisterConnection"/> 动态注入。
/// </summary>
public sealed class TenantDbConnectionDescriptor
{
    /// <summary>SqlSugar ConfigId 标识。建议形如 <c>tenant_{TenantId}</c>。</summary>
    public string ConfigId { get; init; } = string.Empty;

    /// <summary>完整连接串（明文）。调用方负责解密后再传入。</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>数据库类型名，例如 "MySql"。null/空 时使用主库 DbType。</summary>
    public string? DbType { get; init; }

    /// <summary>可选描述（用于运维日志标识）。</summary>
    public string? Description { get; init; }
}

/// <summary>
/// SaaS 多库路由器。切换当前请求的数据库连接到目标 ConfigId。
/// <c>Database.Features.SaasMultiDb.Enabled=false</c> 时所有操作抛 <see cref="NotSupportedException"/>。
/// </summary>
public interface ITenantDbRouter
{
    /// <summary>是否启用多库路由。</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 获取所有已注册的 ConfigId 列表（含 db.json 静态注册 + 运行时动态注入）。
    /// Disabled 时返回空列表。
    /// </summary>
    IReadOnlyList<string> GetAvailableConfigIds();

    /// <summary>
    /// 切换当前 scope 的数据库连接到指定 ConfigId。
    /// 切换后同一请求 scope 内的后续操作均路由到目标库。
    /// </summary>
    /// <param name="configId">目标 ConfigId（必须已注册）。</param>
    /// <exception cref="NotSupportedException">SaasMultiDb 未启用。</exception>
    /// <exception cref="ArgumentException">configId 未注册。</exception>
    void ChangeDatabase(string configId);

    /// <summary>
    /// 获取当前 scope 活跃的 ConfigId。Disabled 时返回 null。
    /// </summary>
    string? CurrentConfigId { get; }

    /// <summary>
    /// 动态注册一个租户库连接。如果该 ConfigId 已存在则按"先注销再注册"覆盖。
    /// 一旦注册成功，<see cref="ChangeDatabase"/> 即可立即切到该 ConfigId。
    /// </summary>
    /// <param name="descriptor">连接描述（明文连接串）。</param>
    /// <exception cref="NotSupportedException">SaasMultiDb 未启用。</exception>
    /// <exception cref="ArgumentException">descriptor 字段不完整。</exception>
    void RegisterConnection(TenantDbConnectionDescriptor descriptor);

    /// <summary>
    /// 注销一个已经注册的 ConfigId（如租户被删除时调用）。
    /// 当前 scope 已切换到该 ConfigId 的请求**不会**回滚——调用方应保证时机。
    /// Disabled 时返回 false。
    /// </summary>
    bool UnregisterConnection(string configId);

    /// <summary>判断给定 ConfigId 是否已注册。Disabled 时始终返回 false。</summary>
    bool Exists(string configId);
}
