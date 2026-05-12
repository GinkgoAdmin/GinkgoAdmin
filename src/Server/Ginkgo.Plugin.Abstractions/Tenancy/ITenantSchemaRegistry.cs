// 文件功能说明：
// 「租户实体注册表」抽象：在所有插件加载完毕后，框架/插件通过该接口枚举
// 应当在租户库中创建/维护的实体类型，供 TenantProvisioningService 调用 CodeFirst.InitTables。

namespace Ginkgo.Plugin.Abstractions.Tenancy;

/// <summary>
/// 租户实体注册表。负责扫描全局已加载程序集，按 <see cref="TenantScopedAttribute"/>/<see cref="SystemSharedAttribute"/>
/// 等标记把实体归类，供建库/同步流程使用。
/// </summary>
public interface ITenantSchemaRegistry
{
    /// <summary>
    /// 触发一次全量扫描；多次调用应保持幂等。模块加载完成后由 Tenant 插件在 OnLoadAsync 调用。
    /// </summary>
    void Rescan();

    /// <summary>
    /// 获取所有应当在租户库中创建的实体类型（即打了 <see cref="TenantScopedAttribute"/> 的类型）。
    /// </summary>
    IReadOnlyList<Type> GetTenantScopedEntities();

    /// <summary>
    /// 获取所有显式声明为「系统共享」的实体类型。
    /// </summary>
    IReadOnlyList<Type> GetSystemSharedEntities();

    /// <summary>
    /// 获取所有启用了租户 + 时间分表的实体类型。
    /// </summary>
    IReadOnlyList<Type> GetTenantTimeSplitEntities();

    /// <summary>
    /// 判断指定实体类型是否属于「租户私有」。
    /// </summary>
    bool IsTenantScoped(Type entityType);
}
