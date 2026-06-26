// 文件功能说明：
// 定义数据字典分类实体。

using SqlSugar;

namespace Ginkgo.Domain.Dictionaries;

/// <summary>
/// 字典分类实体。
/// </summary>
[SugarTable("ginkgo_Sys_Dictionary", TableDescription = "数据字典分类表（分类编码唯一，支持启用/系统内置标记）")]
[SugarIndex("IX_DictionaryCategory_Code", nameof(Code), OrderByType.Asc, true)]
[SugarIndex("IX_DictCategory_Module", nameof(Module), OrderByType.Asc)]
[SugarIndex("IX_DictCategory_Enabled_CreatedAt", $"{nameof(Enabled)},{nameof(CreatedAt)}", OrderByType.Asc)]
public sealed class DictionaryCategory : AuditableEntity
{
    /// <summary>
    /// 所属模块标识：sys = 主框架系统级，其他为插件 module.json 中的 id。
    /// 用于插件卸载时按模块快速定位并清理字典。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "所属模块标识（sys=系统级，其他为插件ModuleId）", DefaultValue = "sys")]
    public string Module { get; set; } = "sys";

    /// <summary>
    /// 分类编码（唯一）。
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "分类编码（唯一）")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 分类名称。
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false, ColumnDescription = "分类名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 分类名称-多语言 JSON
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "分类名称-多语言")]
    public string? NameI18n { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否系统内置。
    /// </summary>
    [SugarColumn(ColumnName = "IsSystem", ColumnDescription = "是否系统内置，不允许删除")]
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// 字典类型（STATIC/DYNAMIC/MAPPING/HIERARCHY/CONFIG/MULTI_LANG/REFERENCE）。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "字典类型：STATIC/DYNAMIC/MAPPING/HIERARCHY/CONFIG/MULTI_LANG/REFERENCE")]
    public string? Category { get; set; }

    /// <summary>
    /// 来源类型或数据源标识（可选）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "来源类型或数据源标识")]
    public string? SourceType { get; set; }

    /// <summary>
    /// 描述。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true, ColumnDescription = "描述")]
    public string? Description { get; set; }

    /// <summary>
    /// 描述-多语言 JSON
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "描述-多语言")]
    public string? DescriptionI18n { get; set; }

    /// <summary>
    /// 扩展 JSON（多语言或映射配置等）。
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "扩展 JSON（多语言/映射配置等）")]
    public string? ExtraJson { get; set; }

    // ===== 领域行为 =====
    public static DictionaryCategory Create(string code, string name, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("分类编码不能为空", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("分类名称不能为空", nameof(name));
        return new DictionaryCategory
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            Code = code.Trim(),
            Name = name.Trim(),
            Enabled = true,
            IsSystem = isSystem
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("分类名称不能为空", nameof(name));
        Name = name.Trim();
    }
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void ChangeMeta(string? category, string? sourceType, string? description, string? extraJson)
    {
        Category = string.IsNullOrWhiteSpace(category) ? null : category!.Trim();
        // 数据库列不允许为 NULL，这里将空值标准化为空字符串
        SourceType = string.IsNullOrWhiteSpace(sourceType) ? string.Empty : sourceType!.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim();
        ExtraJson = string.IsNullOrWhiteSpace(extraJson) ? null : extraJson!.Trim();
    }
}






