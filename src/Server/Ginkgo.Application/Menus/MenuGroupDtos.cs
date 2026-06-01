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
    /// 模块归属：主框架为 'sys'，插件为其 module.json 的 Id（区分大小写）。
    /// 用于按模块过滤与插件卸载时精确清理入口项。
    /// </summary>
    public string Module { get; set; } = "sys";

    /// <summary>
    /// 是否需要授权：false=对所有登录用户公共可见；true=仅超管或经角色授权用户可见。
    /// </summary>
    public bool RequireGrant { get; set; }

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

// ===== 统一客户端入口（Portal）DTO =====

/// <summary>
/// 统一客户端入口树输出（供 WEB_PORTAL / UNIAPP / WPF 三端复用）。
/// 雪花 Id 由既有全局 JSON 配置统一序列化为字符串，前端始终以字符串处理。
/// </summary>
public sealed class ClientPortalDto
{
    /// <summary>
    /// 归一化后的终端类型（UNIAPP / WPF / WEB_PORTAL）。
    /// </summary>
    public string ClientType { get; set; } = string.Empty;

    /// <summary>
    /// 该端默认菜单组 Id（无默认组时为 null）。
    /// </summary>
    public long? GroupId { get; set; }

    /// <summary>
    /// 当前用户可见的入口项（树形）。
    /// </summary>
    public List<ClientPortalItemDto> Items { get; set; } = new();
}

/// <summary>
/// 统一客户端入口项（前端渲染用，只包含入口展示与跳转所需字段）。
/// </summary>
public sealed class ClientPortalItemDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>
    /// 入口跳转地址（对应入口声明的 path）。
    /// </summary>
    public string? Url { get; set; }

    public string? Badge { get; set; }
    public string? BadgeType { get; set; }
    public int Order { get; set; }

    /// <summary>
    /// 是否需要授权：false=对所有登录用户公共可见；true=仅超管或经授权用户可见。
    /// </summary>
    public bool RequireGrant { get; set; }

    /// <summary>
    /// 模块归属：主框架为 'sys'，插件为其 module.json 的 Id（区分大小写）。
    /// </summary>
    public string Module { get; set; } = "sys";

    /// <summary>
    /// 子入口项列表（树形）。
    /// </summary>
    public List<ClientPortalItemDto>? Children { get; set; }
}

/// <summary>
/// 可授权入口（角色编辑器用，按各端默认菜单组分组）。
/// </summary>
public sealed class GrantableMenuItemDto
{
    /// <summary>
    /// 终端类型（UNIAPP / WPF / WEB_PORTAL）。
    /// </summary>
    public string ClientType { get; set; } = string.Empty;

    /// <summary>
    /// 默认菜单组 Id。
    /// </summary>
    public long GroupId { get; set; }

    /// <summary>
    /// 默认菜单组名称。
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// 该默认组下的可授权入口项（树形）。
    /// </summary>
    public List<GrantableItemNodeDto> Items { get; set; } = new();
}

/// <summary>
/// 可授权入口节点（角色编辑器树节点）。
/// </summary>
public sealed class GrantableItemNodeDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>
    /// 是否需要授权：false 时前端标记“公共可见、无需勾选”并禁用勾选。
    /// </summary>
    public bool RequireGrant { get; set; }

    /// <summary>
    /// 模块归属：主框架为 'sys'，插件为其 module.json 的 Id（区分大小写）。
    /// </summary>
    public string Module { get; set; } = "sys";

    public int Order { get; set; }

    /// <summary>
    /// 子节点列表（树形）。
    /// </summary>
    public List<GrantableItemNodeDto>? Children { get; set; }
}

/// <summary>
/// 角色菜单组项（item 级）授权设置输入（全量覆盖）。
/// </summary>
public sealed class SetRoleMenuGroupItemsInput
{
    public long RoleId { get; set; }
    public List<long> MenuGroupItemIds { get; set; } = new();
}

// ===== 安装链路客户端入口声明（install.json 的 ClientMenus.items 同形） =====

/// <summary>
/// 客户端入口声明项（应用层规格类型）。
/// 由插件安装链路（<c>ModuleSqlExecutor</c>）从 <c>install.json</c> 的 <c>ClientMenus.items</c>
/// 解析并映射为本类型后，传入 <see cref="IMenuGroupAppService.UpsertClientMenuItemsAsync"/> 注入入口项。
/// 字段与设计文档《Components and Interfaces 3.3》保持一致（Title/Icon/Path/RequireGrant/Order/Badge）。
/// 注意：该类型定义在应用层，安装链路（Ginkgo.Api）反向依赖应用层并构造此规格调用注入方法，
/// 避免应用层接口反向引用表现层类型造成错误的依赖方向。
/// </summary>
public sealed class ClientMenuItemSpec
{
    /// <summary>
    /// 入口标题（写入 <c>MenuGroupItem.Title</c>）。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 入口图标（写入 <c>MenuGroupItem.Icon</c>），可选。
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 入口跳转地址（写入 <c>MenuGroupItem.Url</c>，对应声明的 path），并作为同一入口项的稳定标识之一。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 是否需要授权（写入 <c>MenuGroupItem.RequireGrant</c>）：false=公共可见，true=需授权可见。
    /// </summary>
    public bool RequireGrant { get; set; }

    /// <summary>
    /// 排序号（写入 <c>MenuGroupItem.Order</c>）。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 角标文案（写入 <c>MenuGroupItem.Badge</c>），可选。
    /// </summary>
    public string? Badge { get; set; }

    /// <summary>
    /// 父级入口的 <c>Path</c>（即父项的 <see cref="Path"/>）。用于在同一终端默认菜单组内构建多级入口树：
    /// 为空表示顶级入口；非空时注入逻辑会按「同组同模块下 <c>Url==ParentPath</c>」解析出父项并写入 <c>ParentId</c>。
    /// 父子可在同一 <c>ClientMenus.items</c> 批次中声明，声明先后顺序不限。
    /// </summary>
    public string? ParentPath { get; set; }
}
