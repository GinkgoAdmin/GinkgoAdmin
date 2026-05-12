// 文件功能说明：
// 定义菜单组模块的 DTO（数据传输对象）。

using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Menus;

// ===== 菜单组 DTO =====

/// <summary>
/// 菜单组列表项输出。
/// </summary>
public sealed class MenuGroupListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ClientType { get; set; }
    public bool IsSystem { get; set; }
    public bool Enabled { get; set; }
    public int MaxDepth { get; set; }
    public string? Version { get; set; }
    /// <summary>
    /// 菜单组下菜单项数量。
    /// </summary>
    public int ItemCount { get; set; }
}

/// <summary>
/// 菜单组详情输出。
/// </summary>
public sealed class MenuGroupDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ClientType { get; set; }
    public bool IsSystem { get; set; }
    public bool Enabled { get; set; }
    public int MaxDepth { get; set; }
    public string? Version { get; set; }
}

/// <summary>
/// 创建菜单组输入。
/// </summary>
public sealed class CreateMenuGroupInput
{
    [Required(ErrorMessage = "菜单组名称不能为空")]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "菜单组标识不能为空")]
    [MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    [MaxLength(64)]
    public string? Location { get; set; }

    [MaxLength(64)]
    public string? ClientType { get; set; }

    public int MaxDepth { get; set; } = 3;

    [MaxLength(32)]
    public string? Version { get; set; }
}

/// <summary>
/// 更新菜单组输入。
/// </summary>
public sealed class UpdateMenuGroupInput
{
    [Required(ErrorMessage = "菜单组名称不能为空")]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "菜单组标识不能为空")]
    [MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    [MaxLength(64)]
    public string? Location { get; set; }

    [MaxLength(64)]
    public string? ClientType { get; set; }

    public bool Enabled { get; set; } = true;

    public int MaxDepth { get; set; } = 3;

    [MaxLength(32)]
    public string? Version { get; set; }
}

// ===== 菜单组项 DTO =====

/// <summary>
/// 菜单组项列表/树节点输出。
/// </summary>
public sealed class MenuGroupItemDto
{
    public long Id { get; set; }
    public long MenuGroupId { get; set; }
    public long? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleI18n { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
    public string? Image { get; set; }
    public string LinkType { get; set; } = "Custom";
    public string? Url { get; set; }
    public string Target { get; set; } = "_self";
    public long? RefMenuId { get; set; }
    /// <summary>
    /// 关联系统菜单名称（RefMenuId 非空时填充）。
    /// </summary>
    public string? RefMenuName { get; set; }
    public string? PermissionCode { get; set; }
    public string? CssClass { get; set; }
    public string? Badge { get; set; }
    public string? BadgeType { get; set; }
    public string? ExtraData { get; set; }
    public int Order { get; set; }
    public bool Enabled { get; set; }
    /// <summary>
    /// 子菜单项列表（树形）。
    /// </summary>
    public List<MenuGroupItemDto>? Children { get; set; }
}

/// <summary>
/// 创建菜单组项输入。
/// </summary>
public sealed class CreateMenuGroupItemInput
{
    [Required(ErrorMessage = "菜单项标题不能为空")]
    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    public string? TitleI18n { get; set; }

    [MaxLength(256)]
    public string? Subtitle { get; set; }

    [MaxLength(64)]
    public string? Icon { get; set; }

    [MaxLength(512)]
    public string? Image { get; set; }

    [Required]
    [MaxLength(16)]
    public string LinkType { get; set; } = "Custom";

    [MaxLength(512)]
    public string? Url { get; set; }

    [MaxLength(16)]
    public string Target { get; set; } = "_self";

    public long? ParentId { get; set; }
    public long? RefMenuId { get; set; }

    [MaxLength(128)]
    public string? PermissionCode { get; set; }

    [MaxLength(128)]
    public string? CssClass { get; set; }

    [MaxLength(32)]
    public string? Badge { get; set; }

    [MaxLength(16)]
    public string? BadgeType { get; set; }

    public string? ExtraData { get; set; }

    public int Order { get; set; }
}

/// <summary>
/// 更新菜单组项输入。
/// </summary>
public sealed class UpdateMenuGroupItemInput
{
    [Required(ErrorMessage = "菜单项标题不能为空")]
    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    public string? TitleI18n { get; set; }

    [MaxLength(256)]
    public string? Subtitle { get; set; }

    [MaxLength(64)]
    public string? Icon { get; set; }

    [MaxLength(512)]
    public string? Image { get; set; }

    [Required]
    [MaxLength(16)]
    public string LinkType { get; set; } = "Custom";

    [MaxLength(512)]
    public string? Url { get; set; }

    [MaxLength(16)]
    public string Target { get; set; } = "_self";

    public long? ParentId { get; set; }
    public long? RefMenuId { get; set; }

    [MaxLength(128)]
    public string? PermissionCode { get; set; }

    [MaxLength(128)]
    public string? CssClass { get; set; }

    [MaxLength(32)]
    public string? Badge { get; set; }

    [MaxLength(16)]
    public string? BadgeType { get; set; }

    public string? ExtraData { get; set; }

    public int Order { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 批量排序输入项。
/// </summary>
public sealed class MenuGroupItemSortInput
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public int Order { get; set; }
}

// ===== 导航查询输出 =====

/// <summary>
/// 导航菜单公开查询输出（按 Slug 查询，前端渲染用）。
/// </summary>
public sealed class NavigationMenuDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Version { get; set; }
    public List<NavigationItemDto> Items { get; set; } = new();
}

/// <summary>
/// 导航菜单项（前端渲染用，只包含展示所需字段）。
/// </summary>
public sealed class NavigationItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleI18n { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
    public string? Image { get; set; }
    public string? Url { get; set; }
    public string Target { get; set; } = "_self";
    public string? CssClass { get; set; }
    public string? Badge { get; set; }
    public string? BadgeType { get; set; }
    public string? ExtraData { get; set; }
    public List<NavigationItemDto>? Children { get; set; }
}

// ===== 角色菜单组权限 DTO =====

/// <summary>
/// 角色菜单组权限设置输入。
/// </summary>
public sealed class SetRoleMenuGroupsInput
{
    public long RoleId { get; set; }
    public List<long> MenuGroupIds { get; set; } = new();
}
