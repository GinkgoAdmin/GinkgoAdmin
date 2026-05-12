// 文件功能说明：
// 标记实体为「按租户 + 时间」二维分表：物理表名形如 {base}_t{tenantId}_{yyyyMM}。
// 由 Tenant 模块的 TenantTimeSplitService（ISplitTableService）在运行时拼接表名并按需自动建表。

namespace Ginkgo.Plugin.Abstractions.Tenancy;

/// <summary>
/// 分表周期：按月、按季、按年。
/// </summary>
public enum TenantSplitInterval
{
    Month = 0,
    Quarter = 1,
    Year = 2,
    Day = 3
}

/// <summary>
/// 标记实体启用「租户 + 时间」二维分表。配合 <see cref="TenantScopedAttribute"/> 使用。
/// </summary>
/// <remarks>
/// - <see cref="BaseName"/>：基础表名，最终物理表名为 <c>{BaseName}_t{TenantId}_{yyyyMM}</c>。
/// - <see cref="Interval"/>：时间分片粒度。
/// - 实体需带 SqlSugar 的 <c>[SplitField]</c> 标注分片字段（推荐 CreatedAt）；未标注时默认按当前时间。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TenantTimeSplitAttribute : Attribute
{
    public string BaseName { get; }

    public TenantSplitInterval Interval { get; }

    public TenantTimeSplitAttribute(string baseName, TenantSplitInterval interval = TenantSplitInterval.Month)
    {
        if (string.IsNullOrWhiteSpace(baseName))
            throw new ArgumentException("baseName 不能为空", nameof(baseName));
        BaseName = baseName;
        Interval = interval;
    }
}
