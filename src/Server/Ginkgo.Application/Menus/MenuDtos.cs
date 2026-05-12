// 文件功能说明：
// 定义菜单模块的 DTO。

using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Menus;

/// <summary>
/// 菜单列表项输出。
/// </summary>
public sealed class MenuListItemDto
{
    /// <summary>
    /// 主键（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 路由。
    /// </summary>
    public string Route { get; set; } = string.Empty;
    /// <summary>
    /// 类型。
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// 图标。
    /// </summary>
    public string? Icon { get; set; }

    // 多客户端与显示模式
    public string? SupportedClients { get; set; }
    public string? WebUrl { get; set; }
    public string? MobileUrl { get; set; }

    // 每客户端显示模式
    public string? WpfDisplayMode { get; set; }
    public string? WebDisplayMode { get; set; }
    public string? MobileDisplayMode { get; set; }

    // 每客户端最终地址
    public string? WpfRouteUrl { get; set; }
    public string? WebRouteUrl { get; set; }
    public string? MobileRouteUrl { get; set; }

}

/// <summary>
/// 菜单详情输出。
/// </summary>
public sealed class MenuDetailDto
{
    /// <summary>
    /// 主键（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 路由。
    /// </summary>
    public string Route { get; set; } = string.Empty;
    /// <summary>
    /// 类型 (Directory/Item/Button)。
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// 打开方式(Tab/Link)。
    /// </summary>
    public string? ItemMode { get; set; }
    /// <summary>
    /// 图标。
    /// </summary>
    public string? Icon { get; set; }

    // 多客户端与显示模式
    public string? SupportedClients { get; set; }
    public string? WebUrl { get; set; }
    public string? MobileUrl { get; set; }

    // 每客户端显示模式
    public string? WpfDisplayMode { get; set; }
    public string? WebDisplayMode { get; set; }
    public string? MobileDisplayMode { get; set; }

    // 每客户端最终地址
    public string? WpfRouteUrl { get; set; }
    public string? WebRouteUrl { get; set; }
    public string? MobileRouteUrl { get; set; }


    /// <summary>
    /// 外部链接。
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 父级 Id（Snowflake ID）。
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 排序。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }

    // 统一资源扩展
    public string? Code { get; set; }
    public string? Resource { get; set; }
    public string? Method { get; set; }
}

/// <summary>
/// 菜单创建输入。
/// </summary>
public sealed class CreateMenuInput
{
    /// <summary>
    /// 名称。
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 路由。
    /// </summary>
    [MaxLength(256)]
    public string Route { get; set; } = string.Empty; // Type=Menu/Directory 可为空

    /// <summary>
    /// 类型。
    /// </summary>
    [Required]
    [MaxLength(16)]
    public string Type { get; set; } = "Directory";

    /// <summary>
    /// 打开方式（可空，仅 Type=Item 有效）。
    /// </summary>
    [MaxLength(16)]
    public string? ItemMode { get; set; }

    /// <summary>
    /// 图标（可空）。
    /// </summary>
    [MaxLength(64)]
    public string? Icon { get; set; }

    // 多客户端与显示模式
    [MaxLength(100)]
    public string? SupportedClients { get; set; }
    [MaxLength(500)]
    public string? WebUrl { get; set; }
    [MaxLength(500)]
    public string? MobileUrl { get; set; }

    // 每客户端显示模式
    [MaxLength(20)]
    public string? WpfDisplayMode { get; set; }
    [MaxLength(20)]
    public string? WebDisplayMode { get; set; }
    [MaxLength(20)]
    public string? MobileDisplayMode { get; set; }

    // 每客户端最终地址（可任一：路由/URL/外部应用）
    [MaxLength(500)]
    public string? WpfRouteUrl { get; set; }
    [MaxLength(500)]
    public string? WebRouteUrl { get; set; }
    [MaxLength(500)]
    public string? MobileRouteUrl { get; set; }


    /// <summary>
    /// 外部链接（可空，仅 Link 有效）。
    /// </summary>
    [MaxLength(512)]
    public string? Url { get; set; }

    /// <summary>
    /// 父级 Id（Snowflake ID）。
    /// </summary>
    public long? ParentId { get; set; }

    // 统一资源扩展
    [MaxLength(256)]
    public string? Code { get; set; }
    [MaxLength(512)]
    public string? Resource { get; set; }
    [MaxLength(16)]
    public string? Method { get; set; }
}

/// <summary>
/// 菜单更新输入。
/// </summary>
public sealed class UpdateMenuInput
{
    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 路由。
    /// </summary>
    public string Route { get; set; } = string.Empty;
    /// <summary>
    /// 类型。
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// 打开方式。
    /// </summary>
    public string? ItemMode { get; set; }
    /// <summary>
    /// 图标。
    /// </summary>
    public string? Icon { get; set; }

    // 多客户端与显示模式
    public string? SupportedClients { get; set; }
    public string? DisplayMode { get; set; } // legacy
    public string? WebUrl { get; set; }
    public string? MobileUrl { get; set; }

    // 每客户端显示模式
    public string? WpfDisplayMode { get; set; }
    public string? WebDisplayMode { get; set; }
    public string? MobileDisplayMode { get; set; }

    // 每客户端最终地址（可任一：路由/URL/外部应用）
    public string? WpfRouteUrl { get; set; }
    public string? WebRouteUrl { get; set; }
    public string? MobileRouteUrl { get; set; }


    /// <summary>
    /// 外部链接。
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 排序。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }

    // 统一资源扩展
    public string? Code { get; set; }
    public string? Resource { get; set; }
    public string? Method { get; set; }
}


