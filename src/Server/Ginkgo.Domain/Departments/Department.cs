using SqlSugar;

namespace Ginkgo.Domain.Departments;

/// <summary>
/// 部门实体。
/// </summary>
[SugarTable("ginkgo_Sys_Department")]
[SugarIndex("IX_Department_ParentId", nameof(ParentId), OrderByType.Asc)]
public sealed class Department : Ginkgo.Domain.AuditableEntity
{
    /// <summary>
    /// 父级部门 Id。
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 部门名称。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 部门编码。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Code { get; set; }

    /// <summary>
    /// 负责人用户 Id。
    /// </summary>
    public long? LeaderUserId { get; set; }

    /// <summary>
    /// 排序号。
    /// </summary>
    [SugarColumn(ColumnName = "OrderNo")]
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;
}




