// 文件功能说明：
// 定义系统角色领域实体。

using SqlSugar;
using System;
using System.Linq;


namespace Ginkgo.Domain.Roles;

/// <summary>
/// 角色实体。
/// </summary>
[SugarTable("ginkgo_Sys_Role")]
[SugarIndex("IX_Role_ParentId", nameof(ParentId), OrderByType.Asc)]
[SugarIndex("IX_Role_Code", nameof(Code), OrderByType.Asc, true)] // 唯一索引
[SugarIndex("IX_Role_Enabled_CreatedAt", $"{nameof(Enabled)},{nameof(CreatedAt)}", OrderByType.Asc)]
public sealed class Role : AuditableEntity
{
    /// <summary>
    /// 父级角色 Id（树形）。
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 角色名称（唯一）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 角色编码（用于权限控制）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 数据范围策略（与 DataScopeType 对应，存储枚举名：All/OwnOnly/DepartmentOnly/DepartmentAndChildren/SpecifiedDepartments/Custom）。
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = false, ColumnName = "DataScope")]
        public string DataScope { get; set; } = "OwnOnly";

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否为超级管理员角色（启用时该角色下所有用户视为超管，跳过所有权限验证）。
    /// </summary>
    public bool IsSuperAdmin { get; set; }

    /// <summary>
    /// 允许登录的客户端列表（逗号分隔：WEB_ADMIN,WEB_PORTAL,WPF,UNIAPP）。
    /// 为 null 或空字符串表示不限制（允许所有客户端）。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true)]
    public string? AllowedClients { get; set; }

    private static readonly string[] AllowedDataScopes = new[] { "All", "OwnOnly", "DepartmentOnly", "DepartmentAndChildren", "SpecifiedDepartments", "Custom" };

    /// <summary>
    /// 设置数据范围（大小写不敏感，非法值回退为 All）。
    /// </summary>
    public void SetDataScope(string? strategy)
    {
        var val = string.IsNullOrWhiteSpace(strategy) ? "OwnOnly" : strategy.Trim();
        var normalized = AllowedDataScopes.FirstOrDefault(x => string.Equals(x, val, StringComparison.OrdinalIgnoreCase));
        DataScope = normalized ?? "OwnOnly";
    }
}






