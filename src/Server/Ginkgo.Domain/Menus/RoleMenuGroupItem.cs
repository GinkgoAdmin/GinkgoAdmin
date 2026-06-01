// 文件功能说明：
// 定义角色—菜单组项（item 级）授权关联实体，与既有的 RoleMenuGroup（组级授权）平行共存。

using SqlSugar;

namespace Ginkgo.Domain.Menus;

/// <summary>
/// 角色菜单组项授权关联（控制角色可访问哪些需授权的菜单组项）。
/// 与组级授权 <see cref="RoleMenuGroup"/> 平行存在，用于 item 级的细粒度授权。
/// </summary>
[SugarTable("ginkgo_Sys_RoleMenuGroupItem", TableDescription = "角色菜单组项授权表")]
[SugarIndex("UK_RoleMenuGroupItem", $"{nameof(RoleId)},{nameof(MenuGroupItemId)}", OrderByType.Asc, true)]
public sealed class RoleMenuGroupItem : Entity
{
    /// <summary>
    /// 角色 Id。
    /// </summary>
    [SugarColumn(ColumnDescription = "角色Id")]
    public long RoleId { get; set; }

    /// <summary>
    /// 菜单组项 Id。
    /// </summary>
    [SugarColumn(ColumnDescription = "菜单组项Id")]
    public long MenuGroupItemId { get; set; }

    /// <summary>
    /// 创建时间（服务器本地时间，DATETIME(6)）。
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建人。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "创建人")]
    public long? CreatedBy { get; set; }
}
