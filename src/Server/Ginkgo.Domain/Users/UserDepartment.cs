using SqlSugar;

namespace Ginkgo.Domain.Users;

/// <summary>
/// 用户-部门关系。
/// </summary>
[SugarTable("ginkgo_Sys_UserDepartment")]
[SugarIndex("UX_UserDepartment_User_Department", nameof(UserId)+","+nameof(DepartmentId), OrderByType.Asc, true)] //   
[SugarIndex("IX_UserDepartment_UserId", nameof(UserId), OrderByType.Asc)]
[SugarIndex("IX_UserDepartment_DepartmentId", nameof(DepartmentId), OrderByType.Asc)]
public sealed class UserDepartment : Ginkgo.Domain.Entity
{
    public long UserId { get; set; }
    public long DepartmentId { get; set; }

    [SugarColumn(DefaultValue = "0")]
    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long? CreatedBy { get; set; }

    [SugarColumn(DefaultValue = "0")]
    public bool IsManager { get; set; }
}



