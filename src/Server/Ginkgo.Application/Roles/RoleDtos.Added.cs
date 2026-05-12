// 文件功能说明：
// 定义角色模块的 DTO（基础 CRUD、权限树/分配、数据范围管理）。

using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Roles;

/// <summary>
/// 角色列表项输出。
/// </summary>
public sealed class RoleListItemDto
{
    /// <summary>主键（Snowflake ID）。</summary>
    public long Id { get; set; }
    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>编码。</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; }
    /// <summary>数据范围策略（枚举名：All/OwnOnly/DepartmentOnly/DepartmentAndChildren/SpecifiedDepartments/Custom）。</summary>
    public string DataScope { get; set; } = "OwnOnly";
    /// <summary>允许登录的客户端列表（逗号分隔），NULL=不限制。</summary>
    public string? AllowedClients { get; set; }
    /// <summary>是否为超级管理员角色。</summary>
    public bool IsSuperAdmin { get; set; }
}

public sealed class RoleTreeNodeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string DataScope { get; set; } = "OwnOnly";
    public string? AllowedClients { get; set; }
    public bool IsSuperAdmin { get; set; }
    public List<RoleTreeNodeDto> Children { get; set; } = new();
}

/// <summary>
/// 角色详情输出（用于编辑回显）。
/// </summary>
public sealed class RoleDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public long? ParentId { get; set; }
    public string? AllowedClients { get; set; }
    public bool IsSuperAdmin { get; set; }
}

/// <summary>
/// 角色创建输入。
/// </summary>
public sealed class CreateRoleInput
{
    /// <summary>名称。</summary>
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>编码。</summary>
    [Required]
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>上级角色 Id（可空；不选即顶级）。</summary>
    public long? ParentId { get; set; }

    /// <summary>数据范围策略。</summary>
    [MaxLength(64)]
    public string DataScope { get; set; } = "OwnOnly";

    /// <summary>允许登录的客户端列表（逗号分隔），NULL=不限制。</summary>
    [MaxLength(256)]
    public string? AllowedClients { get; set; }

    /// <summary>是否为超级管理员角色。</summary>
    public bool IsSuperAdmin { get; set; }
}

/// <summary>
/// 角色更新输入。
/// </summary>
public sealed class UpdateRoleInput
{
    /// <summary>名称。</summary>
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>编码。</summary>
    [Required]
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; }

    /// <summary>上级角色 Id（可空；不选即顶级）。</summary>
    public long? ParentId { get; set; }

    /// <summary>数据范围策略。</summary>
    [MaxLength(64)]
    public string DataScope { get; set; } = "OwnOnly";

    /// <summary>允许登录的客户端列表（逗号分隔），NULL=不限制。</summary>
    [MaxLength(256)]
    public string? AllowedClients { get; set; }

    /// <summary>是否为超级管理员角色。</summary>
    public bool IsSuperAdmin { get; set; }
}

public sealed class PermissionItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // API/Menu/Button
}

public sealed class PermissionTreeNodeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Directory/Item/Button
    public string Route { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Resource { get; set; }
    public string? Method { get; set; }
    public long? PermissionId { get; set; } // 若此节点对应权限条目，携带其 Id
    public List<PermissionTreeNodeDto> Children { get; set; } = new();
}

/// <summary>
/// 角色数据范围详情输出。
/// </summary>
public sealed class RoleDataScopeDto
{
    /// <summary>数据范围策略（枚举名：All/OwnOnly/DepartmentOnly/DepartmentAndChildren/SpecifiedDepartments/Custom）。</summary>
    public string DataScope { get; set; } = "OwnOnly";
    /// <summary>指定部门 Id 列表（当 DataScope=SpecifiedDepartments 时有效）。</summary>
    public List<long> DepartmentIds { get; set; } = new();
}

/// <summary>
/// 设置角色数据范围输入。
/// </summary>
public sealed class SetRoleDataScopeInput
{
    /// <summary>数据范围策略（与 DataScopeType 枚举名一致）。</summary>
    [Required]
    [MaxLength(64)]
    public string DataScope { get; set; } = "OwnOnly";

    /// <summary>指定部门 Id 列表（当 DataScope=SpecifiedDepartments 时必填）。</summary>
    public List<long>? DepartmentIds { get; set; }
}
