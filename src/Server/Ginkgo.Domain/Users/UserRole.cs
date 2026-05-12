using SqlSugar;

namespace Ginkgo.Domain.Users;

/// <summary>
/// 用户-角色关系。
/// </summary>
[SugarTable("ginkgo_Sys_UserRole")]
[SugarIndex("UX_UserRole_User_Role", nameof(UserId)+","+nameof(RoleId), OrderByType.Asc, true)] // 唯一索引：避免重复授予同一用户同一角色
[SugarIndex("IX_UserRole_UserId", nameof(UserId), OrderByType.Asc)]
[SugarIndex("IX_UserRole_RoleId", nameof(RoleId), OrderByType.Asc)]
public sealed class UserRole : Entity
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long? CreatedBy { get; set; }
}



