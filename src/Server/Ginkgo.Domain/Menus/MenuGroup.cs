// 文件功能说明：
// 定义菜单组（导航菜单）领域实体。支持创建不同类型版本的自定义导航菜单。

using SqlSugar;

namespace Ginkgo.Domain.Menus;

/// <summary>
/// 菜单组实体（类似 WordPress 菜单组概念）。
/// 用于管理前端导航、页脚链接、手机端菜单等自定义导航菜单。
/// </summary>
[SugarTable("ginkgo_Sys_MenuGroup", TableDescription = "菜单组定义表")]
[SugarIndex("UK_MenuGroup_Slug", nameof(Slug), OrderByType.Asc, true)]
[SugarIndex("IX_MenuGroup_Location", nameof(Location), OrderByType.Asc)]
[SugarIndex("IX_MenuGroup_Enabled", $"{nameof(Enabled)},{nameof(IsDeleted)}", OrderByType.Asc)]
public sealed class MenuGroup : AuditableEntity
{
    /// <summary>
    /// 菜单组名称。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "菜单组名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 唯一标识（程序调用用，如 frontend-nav、footer）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "唯一标识")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 描述说明。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true, ColumnDescription = "描述说明")]
    public string? Description { get; set; }

    /// <summary>
    /// 展示位置标识（site-header / mobile-tabbar / site-footer 等）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "展示位置标识")]
    public string? Location { get; set; }

    /// <summary>
    /// 适用终端类型（WEB_ADMIN/WEB_PORTAL/WPF/UNIAPP，逗号分隔）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "适用终端类型")]
    public string? ClientType { get; set; }

    /// <summary>
    /// 是否系统内置（内置菜单组不可删除）。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否系统内置")]
    public bool IsSystem { get; set; }

    /// <summary>
    /// 是否为该终端类型的默认菜单组（每个 ClientType 下唯一）。
    /// 插件业务入口仅注入到默认菜单组；角色授权界面仅展示默认菜单组。
    /// 布尔语义列，MySQL 映射为 TINYINT(1)，默认 false。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否为该终端类型的默认菜单组")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大嵌套层级（0=不限制）。
    /// </summary>
    [SugarColumn(ColumnDescription = "最大嵌套层级")]
    public int MaxDepth { get; set; } = 3;

    /// <summary>
    /// 版本标识（v1/v2/beta，同 Location 可多版本）。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "版本标识")]
    public string? Version { get; set; }

    // ===== 领域行为 =====

    /// <summary>
    /// 工厂方法：创建菜单组。
    /// isSystem：是否系统内置（内置菜单组不可删除），默认 false；
    /// isDefault：是否为该终端类型的默认菜单组，默认 false。
    /// </summary>
    public static MenuGroup Create(string name, string slug, string? description = null,
        string? location = null, string? clientType = null, string? version = null,
        bool isSystem = false, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("菜单组名称不能为空", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("菜单组标识不能为空", nameof(slug));
        return new MenuGroup
        {
            Id = Utils.SnowflakeIdGenerator.NextId(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            Location = location?.Trim(),
            ClientType = clientType?.Trim(),
            Version = version?.Trim(),
            Enabled = true,
            IsSystem = isSystem,
            IsDefault = isDefault,
            MaxDepth = 3
        };
    }

    /// <summary>
    /// 更新菜单组基本信息。
    /// </summary>
    public void UpdateMeta(string name, string slug, string? description, string? location,
        string? clientType, string? version, int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("菜单组名称不能为空", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("菜单组标识不能为空", nameof(slug));
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = description?.Trim();
        Location = location?.Trim();
        ClientType = clientType?.Trim();
        Version = version?.Trim();
        MaxDepth = maxDepth;
    }

    /// <summary>
    /// 启用。
    /// </summary>
    public void Enable() => Enabled = true;

    /// <summary>
    /// 禁用。
    /// </summary>
    public void Disable() => Enabled = false;

    /// <summary>
    /// 标记为该终端类型的默认菜单组（仅切换 IsDefault，唯一性由应用层维护）。
    /// </summary>
    public void MarkAsDefault() => IsDefault = true;

    /// <summary>
    /// 取消默认菜单组标记（仅切换 IsDefault）。
    /// </summary>
    public void UnmarkDefault() => IsDefault = false;

    /// <summary>
    /// 标记为系统内置菜单组（置 IsSystem=true，用于框架预置）。
    /// </summary>
    public void MarkSystem() => IsSystem = true;
}
