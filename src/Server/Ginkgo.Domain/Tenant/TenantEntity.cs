using SqlSugar;

namespace Ginkgo.Domain.Tenant;

/// <summary>
/// 多租户实体基类。继承此类的实体会自动参与租户过滤。
/// TenantId 可为空，兼容无租户场景（单租户部署时所有记录 TenantId 为 null）。
/// </summary>
public abstract class TenantEntity : AuditableEntity
{
    /// <summary>
    /// 租户 ID（可空，兼容单租户部署）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "租户Id（可空，兼容单租户）")]
    public long? TenantId { get; set; }
}
