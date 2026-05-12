// 文件功能说明：
// 定义角色-数据范围指定部门关系实体（用于 DataScopeType.SpecifiedDepartments）。

using SqlSugar;

namespace Ginkgo.Domain.Roles;

/// <summary>
/// 角色-数据范围-指定部门关系。
/// </summary>
[SugarTable("ginkgo_Sys_RoleDataScopeDept")]
[SugarIndex("UX_RoleDataScopeDept_Role_Department", nameof(RoleId)+","+nameof(DepartmentId), OrderByType.Asc, true)]
[SugarIndex("IX_RoleDataScopeDept_RoleId", nameof(RoleId), OrderByType.Asc)]
[SugarIndex("IX_RoleDataScopeDept_DepartmentId", nameof(DepartmentId), OrderByType.Asc)]
public sealed class RoleDataScopeDept : Ginkgo.Domain.Entity
{
    /// <summary>
    /// 角色 Id。
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// 部门 Id。
    /// </summary>
    public long DepartmentId { get; set; }
}

