using System;
using SqlSugar;

namespace Ginkgo.Domain.Roles;

[SugarTable("ginkgo_Sys_RolePermission")]
[SugarIndex("IX_RolePermission_RoleId_PermissionId", $"{nameof(RoleId)},{nameof(PermissionId)}", OrderByType.Asc, true)] // 唯一索引
public sealed class RolePermission : Ginkgo.Domain.Entity
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }

    [SugarColumn(ColumnDescription = "创建时间（UTC）")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsNullable = true, ColumnDescription = "创建人用户Id")]
    public long? CreatedBy { get; set; }
}




