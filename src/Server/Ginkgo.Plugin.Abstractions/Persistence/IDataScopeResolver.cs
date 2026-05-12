namespace Ginkgo.Infrastructure.Persistence;

/// <summary>
/// 数据范围类型（可按角色组合并取并集）。
/// </summary>
public enum DataScopeType
{
    /// <summary>全部数据（如 ADMIN）。</summary>
    All = 0,
    /// <summary>仅本人数据（CreatedBy == 当前用户）。</summary>
    OwnOnly = 1,
    /// <summary>本部门。</summary>
    DepartmentOnly = 2,
    /// <summary>本部门及下级部门。</summary>
    DepartmentAndChildren = 3,
    /// <summary>指定部门（从配置读取）。</summary>
    SpecifiedDepartments = 4,
    /// <summary>自定义/扩展（预留）。</summary>
    Custom = 9,
}

/// <summary>
/// 数据范围配置（读取 appsettings:DataScope 节）。
/// 通过配置实现"无代码变更"的策略调整与角色映射。
/// </summary>
public sealed class DataScopeOptions
{
    /// <summary>是否启用自动数据范围过滤（默认 false，保证兼容）。</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>默认策略（当角色未配置时使用）。</summary>
    public DataScopeType DefaultStrategy { get; set; } = DataScopeType.DepartmentAndChildren;
    /// <summary>角色到策略的映射（键：角色码或名称，大小写不敏感）。</summary>
    public Dictionary<string, DataScopeType> RoleStrategies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>角色到"指定部门列表"的映射（仅当策略为 SpecifiedDepartments 时生效）。</summary>
    public Dictionary<string, List<long>> RoleSpecifiedDepartments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>识别为"超级管理员"的角色集合（命中则视为 All）。</summary>
    public HashSet<string> AdminRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase) { "ADMIN", "SUPERADMIN" };
}

/// <summary>
/// 计算得到的有效数据范围描述。
/// </summary>
public sealed class EffectiveDataScope
{
    public bool IsAll { get; set; }
    public bool IncludeOwn { get; set; }
    public HashSet<long> DepartmentIds { get; } = new();
}

/// <summary>
/// 数据范围解析器抽象接口。
/// 基于当前用户角色与配置计算可访问的数据范围。
/// </summary>
public interface IDataScopeResolver
{
    /// <summary>
    /// 根据当前用户与配置计算有效数据范围。
    /// </summary>
    EffectiveDataScope Resolve();

    /// <summary>
    /// 数据范围过滤是否启用。
    /// 默认实现返回 true，由具体实现根据系统配置（DB Settings 或 appsettings）决定。
    /// 主框架的 SqlSugarRepository 等基础设施依赖此开关决定是否对查询追加范围 WHERE。
    /// </summary>
    bool IsEnabled() => true;
}
