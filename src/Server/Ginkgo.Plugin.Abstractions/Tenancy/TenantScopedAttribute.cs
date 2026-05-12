// 文件功能说明：
// 标记实体为「租户私有」。当框架启用 SeparateDb 模式时，这些实体的物理表只在租户库里创建，
// 主库不会出现；CodeFirst.InitTables 由 ITenantSchemaRegistry 在租户库中调度。

namespace Ginkgo.Plugin.Abstractions.Tenancy;

/// <summary>
/// 标记实体为「租户私有表」：仅在 SeparateDb 模式的租户库中创建；Shared 模式下回退到主库 + TenantId 列过滤。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TenantScopedAttribute : Attribute
{
    /// <summary>
    /// 可选：声明实体所属的业务域，用于运维侧分组展示（不参与运行时分库逻辑）。
    /// </summary>
    public string? Category { get; set; }

    public TenantScopedAttribute() { }

    public TenantScopedAttribute(string category)
    {
        Category = category;
    }
}
