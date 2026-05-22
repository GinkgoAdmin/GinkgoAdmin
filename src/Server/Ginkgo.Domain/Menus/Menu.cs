// 文件功能说明：
// 定义系统菜单领域实体。

using SqlSugar;

namespace Ginkgo.Domain.Menus;

/// <summary>
/// 菜单实体。
/// </summary>
[SugarTable("ginkgo_Sys_Menu", TableDescription = "系统菜单表（支持目录/菜单/按钮/API，包含启用状态与排序）")]
[SugarIndex("IX_Menu_ParentId_Order", $"{nameof(ParentId)},{nameof(Order)}", OrderByType.Asc)]
[SugarIndex("IX_Menu_Type", nameof(Type), OrderByType.Asc)]
[SugarIndex("IX_Menu_Code", nameof(Code), OrderByType.Asc)]
[SugarIndex("IX_Menu_Module", nameof(Module), OrderByType.Asc)]
[SugarIndex("IX_Menu_Enabled_CreatedAt", $"{nameof(Enabled)},{nameof(CreatedAt)}", OrderByType.Asc)]
public sealed class Menu : AuditableEntity
{
    /// <summary>
    /// 所属模块标识：sys = 主框架系统级，其他为插件 module.json 中的 id。
    /// 用于插件卸载时按模块快速定位并清理菜单。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "所属模块标识（sys=系统级，其他为插件ModuleId）", DefaultValue = "sys")]
    public string Module { get; set; } = "sys";

    /// <summary>
    /// 菜单名称。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "菜单名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 菜单名称-多语言 JSON，格式: {"zh-CN":"系统管理","en":"System"}
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "菜单名称-多语言")]
    public string? NameI18n { get; set; }

    /// <summary>
    /// 路由或标识。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true, ColumnDescription = "路由或唯一标识")]
    public string? Route { get; set; }

    /// <summary>
    /// 资源类型：Directory / Menu / Button / Api。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = false, ColumnDescription = "类型：Directory/Menu/Button/Api")]
    public string Type { get; set; } = "Directory";

    /// <summary>
    /// 菜单项打开方式：Tab / Link，仅当 Type=Item 时有效。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "菜单项打开方式：Tab/Link")]
    public string? ItemMode { get; set; }

    /// <summary>
    /// 图标。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "图标")]
    public string? Icon { get; set; }

    /// <summary>
    /// 外部链接 URL（ItemMode=Link 时使用）。
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "外部链接URL（ItemMode=Link）")]
    public string? Url { get; set; }

    // ===== 多客户端与显示模式 =====
    [SugarColumn(Length = 100, IsNullable = true, ColumnDescription = "支持的客户端类型：WPF,Web,Mobile")]
    public string? SupportedClients { get; set; }


    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "网页端访问地址")]
    public string? WebUrl { get; set; }

    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "手机端访问地址")]
    public string? MobileUrl { get; set; }

    // 每客户端显示模式（Route/URL/External）
    [SugarColumn(Length = 20, IsNullable = true, ColumnDescription = "WPF客户端显示模式：Route=路由跳转，URL=外部链接，External=外部应用")]
    public string? WpfDisplayMode { get; set; }
    [SugarColumn(Length = 20, IsNullable = true, ColumnDescription = "Web客户端显示模式：Route=路由跳转，URL=外部链接，External=外部应用")]
    public string? WebDisplayMode { get; set; }
    [SugarColumn(Length = 20, IsNullable = true, ColumnDescription = "Mobile客户端显示模式：Route=路由跳转，URL=外部链接，External=外部应用")]
    public string? MobileDisplayMode { get; set; }

    // 每客户端最终地址（Route/URL/External 任一）
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "WPF客户端的地址（可为路由/URL/外部应用）")]
    public string? WpfRouteUrl { get; set; }
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "Web客户端的地址（可为路由/URL/外部应用）")]
    public string? WebRouteUrl { get; set; }
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "Mobile客户端的地址（可为路由/URL/外部应用）")]
    public string? MobileRouteUrl { get; set; }

    // API 模板与方法（按钮/动作API使用）
    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "API资源模板")]
    public string? Resource { get; set; }

    [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "HTTP方法：GET/POST/... ")]
    public string? Method { get; set; }

    /// <summary>
    /// 父级菜单 Id。
    /// </summary>
    [SugarColumn(ColumnDescription = "父级菜单Id（自关联）")]
    public long? ParentId { get; set; }

    /// <summary>
    /// 排序号。
    /// </summary>
    [SugarColumn(ColumnName = "OrderNo", ColumnDescription = "排序号（同级从小到大）")]
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    [SugarColumn(ColumnName = "Visible", ColumnDescription = "是否启用")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 唯一编码：Menu=Route；Button=完整菜单路由:动作；Api=模块:动作
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true, ColumnDescription = "唯一编码：Menu=Route；Button=完整路由:动作；Api=模块:动作")]
    public string? Code { get; set; }

    // ===== 领域行为 =====
    public static Menu Create(string name, string type, string? route, long? parentId, string? icon, string? url, string? code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("名称不能为空", nameof(name));
        var t = NormalizeType(type);
        return new Menu
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            Name = name.Trim(),
            Type = t,
            Route = string.IsNullOrWhiteSpace(route) ? null : route!.Trim(),
            ParentId = parentId,
            Icon = icon,
            Url = url,
            Code = string.IsNullOrWhiteSpace(code) ? null : code!.Trim(),
            Enabled = true,
            Order = 0
        };
    }

        // Overload: with multi-client fields
        public static Menu Create(string name, string type, string? route, long? parentId, string? icon, string? url, string? code,
                                  string? supportedClients, string? webUrl, string? mobileUrl,
                                  string? wpfDisplayMode, string? webDisplayMode, string? mobileDisplayMode,
                                  string? wpfRouteUrl, string? webRouteUrl, string? mobileRouteUrl)
        {
            var m = Create(name, type, route, parentId, icon, url, code);
            m.SupportedClients = supportedClients;
            m.WebUrl = webUrl;
            m.MobileUrl = mobileUrl;
            // per-client modes
            m.WpfDisplayMode = wpfDisplayMode;
            m.WebDisplayMode = webDisplayMode;
            m.MobileDisplayMode = mobileDisplayMode;
            // per-client final addresses
            m.WpfRouteUrl = wpfRouteUrl;
            m.WebRouteUrl = webRouteUrl;
            m.MobileRouteUrl = mobileRouteUrl;
            return m;
        }

        public void UpdateMeta(string name, string? route, string? icon, string? url, string? code,
                               string? supportedClients, string? webUrl, string? mobileUrl,
                               string? wpfDisplayMode, string? webDisplayMode, string? mobileDisplayMode,
                               string? wpfRouteUrl, string? webRouteUrl, string? mobileRouteUrl)
        {
            UpdateMeta(name, route, icon, url, code);
            SupportedClients = supportedClients;
            WebUrl = webUrl;
            MobileUrl = mobileUrl;
            WpfDisplayMode = wpfDisplayMode;
            WebDisplayMode = webDisplayMode;
            MobileDisplayMode = mobileDisplayMode;
            WpfRouteUrl = wpfRouteUrl;
            WebRouteUrl = webRouteUrl;
            MobileRouteUrl = mobileRouteUrl;
        }


    public void UpdateMeta(string name, string? route, string? icon, string? url, string? code)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        Route = string.IsNullOrWhiteSpace(route) ? null : route!.Trim();
        Icon = icon;
        Url = url;
        Code = string.IsNullOrWhiteSpace(code) ? null : code!.Trim();
    }

    public void MoveTo(long? newParentId) => ParentId = newParentId;
    public void SetOrder(int order) => Order = order < 0 ? 0 : order;
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void SetType(string type) => Type = NormalizeType(type);

    private static string NormalizeType(string type)
    {
        var t = (type ?? "Directory").Trim();
        if (t.Equals("directory", StringComparison.OrdinalIgnoreCase)) return "Directory";
        if (t.Equals("menu", StringComparison.OrdinalIgnoreCase)) return "Menu";
        if (t.Equals("button", StringComparison.OrdinalIgnoreCase)) return "Button";
        if (t.Equals("api", StringComparison.OrdinalIgnoreCase)) return "Api";
        return "Directory";
    }
}





