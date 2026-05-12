// 文件功能说明：
// 定义角色菜单组权限关联实体。

using SqlSugar;

namespace Ginkgo.Domain.Menus;

/// <summary>
/// 角色菜单组权限关联（控制角色可访问哪些菜单组）。
/// </summary>
[SugarTable("ginkgo_Sys_RoleMenuGroup", TableDescription = "角色菜单组权限表")]
[SugarIndex("UK_RoleMenuGroup", $"{nameof(RoleId)},{nameof(MenuGroupId)}", OrderByType.Asc, true)]
public sealed class RoleMenuGroup : Entity
{
    /// <summary>
    /// 角色 Id。
    /// </summary>
    [SugarColumn(ColumnDescription = "角色Id")]
    public long RoleId { get; set; }

    /// <summary>
    /// 菜单组 Id。
    /// </summary>
    [SugarColumn(ColumnDescription = "菜单组Id")]
    public long MenuGroupId { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建人。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "创建人")]
    public long? CreatedBy { get; set; }
}
