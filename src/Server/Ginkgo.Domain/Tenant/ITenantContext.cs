namespace Ginkgo.Domain.Tenant;

/// <summary>
/// 租户上下文接口，提供当前请求的租户 ID。
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// 当前租户 ID。为 null 表示无租户（兼容单租户部署或管理员全局操作）。
    /// </summary>
    long? CurrentTenantId { get; }
}

/// <summary>
/// 默认租户上下文实现（Scoped 生命周期）。
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public long? CurrentTenantId { get; set; }
}
