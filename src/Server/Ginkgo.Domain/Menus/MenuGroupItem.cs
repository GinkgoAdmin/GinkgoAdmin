// 文件功能说明：
// 定义菜单组项（导航菜单项）领域实体。每个菜单项可以是自定义链接、关联系统菜单或外部链接。

using SqlSugar;

namespace Ginkgo.Domain.Menus;

/// <summary>
/// 菜单组项实体（菜单组内的菜单项，支持树形嵌套）。
/// </summary>
[SugarTable("ginkgo_Sys_MenuGroupItem", TableDescription = "菜单组项表")]
[SugarIndex("IX_MenuGroupItem_Group_Parent_Order", $"{nameof(MenuGroupId)},{nameof(ParentId)},{nameof(Order)}", OrderByType.Asc)]
[SugarIndex("IX_MenuGroupItem_RefMenuId", nameof(RefMenuId), OrderByType.Asc)]
[SugarIndex("IX_MenuGroupItem_PermissionCode", nameof(PermissionCode), OrderByType.Asc)]
[SugarIndex("IX_MenuGroupItem_Enabled", $"{nameof(Enabled)},{nameof(IsDeleted)}", OrderByType.Asc)]
[SugarIndex("IX_MenuGroupItem_Module", nameof(Module), OrderByType.Asc)]
public sealed class MenuGroupItem : AuditableEntity
{
    /// <summary>
    /// 所属菜单组 Id。
    /// </summary>
    [SugarColumn(ColumnDescription = "所属菜单组Id")]
    public long MenuGroupId { get; set; }

    /// <summary>
    /// 父级菜单项 Id（树形自关联）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "父级菜单项Id")]
    public long? ParentId { get; set; }

    /// <summary>
    /// 显示标题。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false, ColumnDescription = "显示标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 多语言标题 JSON。
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "多语言标题")]
    public string? TitleI18n { get; set; }

    /// <summary>
    /// 副标题/描述。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true, ColumnDescription = "副标题")]
    public string? Subtitle { get; set; }

    /// <summary>
    /// 图标。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "图标")]
    public string? Icon { get; set; }

    /// <summary>
    /// 图片地址（图片导航场景）。
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "图片地址")]
    public string? Image { get; set; }

    /// <summary>
    /// 链接类型：Custom=自定义链接, SystemMenu=关联系统菜单, External=外部链接。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = false, ColumnDescription = "链接类型：Custom/SystemMenu/External")]
    public string LinkType { get; set; } = "Custom";

    /// <summary>
    /// 链接地址。
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "链接地址")]
    public string? Url { get; set; }

    /// <summary>
    /// 打开方式：_self=当前窗口, _blank=新窗口。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = false, ColumnDescription = "打开方式")]
    public string Target { get; set; } = "_self";

    /// <summary>
    /// 关联的系统菜单 Id（LinkType=SystemMenu 时使用）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "关联系统菜单Id")]
    public long? RefMenuId { get; set; }

    /// <summary>
    /// 权限编码（与系统菜单 PermissionCode 同体系，用于按角色过滤显示）。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "权限编码")]
    public string? PermissionCode { get; set; }

    /// <summary>
    /// 自定义 CSS 类名。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "自定义CSS类")]
    public string? CssClass { get; set; }

    /// <summary>
    /// 角标文字（如 New, Hot, 99+）。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "角标文字")]
    public string? Badge { get; set; }

    /// <summary>
    /// 角标类型：primary/success/warning/danger/info。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "角标类型")]
    public string? BadgeType { get; set; }

    /// <summary>
    /// 扩展数据（JSON 格式）。
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "扩展数据")]
    public string? ExtraData { get; set; }

    /// <summary>
    /// 排序号（同级从小到大）。
    /// </summary>
    [SugarColumn(ColumnName = "OrderNo", ColumnDescription = "排序号")]
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 模块归属：主框架为 'sys'，插件为其 module.json 的 Id（区分大小写）。
    /// 用于插件卸载时按模块精确清理入口项，实现一键归零。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false, ColumnDescription = "模块归属（sys 或插件ModuleId）")]
    public string Module { get; set; } = "sys";

    /// <summary>
    /// 是否需要授权：false=对所有登录用户可见；true=仅超管或经角色授权的用户可见。
    /// 采用独立列表达授权语义，避免与既有后台 RBAC 的 PermissionCode 语义混淆。
    /// 布尔列由 SqlSugar 映射为 MySQL TINYINT(1)。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否需要授权（0=公共可见 1=需授权）")]
    public bool RequireGrant { get; set; }

    // ===== 领域行为 =====

    /// <summary>
    /// 工厂方法：创建菜单组项。
    /// </summary>
    /// <param name="module">模块归属：主框架为 'sys'，插件为其 module.json 的 Id（区分大小写）。</param>
    /// <param name="requireGrant">是否需要授权：false=公共可见，true=需授权可见。</param>
    public static MenuGroupItem Create(long menuGroupId, string title, string linkType = "Custom",
        string? url = null, long? parentId = null, long? refMenuId = null,
        string module = "sys", bool requireGrant = false)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("菜单项标题不能为空", nameof(title));
        var lt = NormalizeLinkType(linkType);
        return new MenuGroupItem
        {
            Id = Utils.SnowflakeIdGenerator.NextId(),
            MenuGroupId = menuGroupId,
            ParentId = parentId,
            Title = title.Trim(),
            LinkType = lt,
            Url = url?.Trim(),
            Target = "_self",
            RefMenuId = refMenuId,
            Enabled = true,
            Order = 0,
            Module = string.IsNullOrWhiteSpace(module) ? "sys" : module.Trim(),
            RequireGrant = requireGrant
        };
    }

    /// <summary>
    /// 更新基本信息。
    /// </summary>
    /// <param name="module">模块归属（null 表示不改动）。</param>
    /// <param name="requireGrant">是否需要授权（null 表示不改动）。</param>
    public void UpdateMeta(string title, string? subtitle, string? icon, string? image,
        string linkType, string? url, string target, long? refMenuId, string? permissionCode,
        string? cssClass, string? badge, string? badgeType, string? extraData,
        string? module = null, bool? requireGrant = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("菜单项标题不能为空", nameof(title));
        Title = title.Trim();
        Subtitle = subtitle?.Trim();
        Icon = icon?.Trim();
        Image = image?.Trim();
        LinkType = NormalizeLinkType(linkType);
        Url = url?.Trim();
        Target = string.IsNullOrWhiteSpace(target) ? "_self" : target.Trim();
        RefMenuId = refMenuId;
        PermissionCode = permissionCode?.Trim();
        CssClass = cssClass?.Trim();
        Badge = badge?.Trim();
        BadgeType = badgeType?.Trim();
        ExtraData = extraData;
        // null 表示不改动，保持既有 Web 管理 UI 调用兼容
        if (module is not null) Module = string.IsNullOrWhiteSpace(module) ? "sys" : module.Trim();
        if (requireGrant.HasValue) RequireGrant = requireGrant.Value;
    }

    /// <summary>
    /// 移动到指定父级。
    /// </summary>
    public void MoveTo(long? parentId) => ParentId = parentId;

    /// <summary>
    /// 设置排序号。
    /// </summary>
    public void SetOrder(int order) => Order = order;

    /// <summary>
    /// 启用。
    /// </summary>
    public void Enable() => Enabled = true;

    /// <summary>
    /// 禁用。
    /// </summary>
    public void Disable() => Enabled = false;

    /// <summary>
    /// 标准化链接类型。
    /// </summary>
    private static string NormalizeLinkType(string linkType)
    {
        return (linkType?.Trim().ToLowerInvariant()) switch
        {
            "systemmenu" => "SystemMenu",
            "external" => "External",
            _ => "Custom"
        };
    }
}
