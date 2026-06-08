// 文件功能说明：
// 定义字典模块的 DTO。

using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Dictionaries;

/// <summary>
/// 字典分类列表项。
/// </summary>
public sealed class DictionaryCategoryListItemDto
{
    /// <summary>
    /// 主键（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 字典类型。
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// 字典分类创建输入。
/// </summary>
public sealed class CreateDictionaryCategoryInput
{
    /// <summary>
    /// 编码。
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 名称。
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字典类型。
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    [MaxLength(50)] public string? SourceType { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    public string? ExtraJson { get; set; }
}

/// <summary>
/// 字典分类更新输入。
/// </summary>
public sealed class UpdateDictionaryCategoryInput
{
    /// <summary>
    /// 名称。
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 字典类型。
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    [MaxLength(50)] public string? SourceType { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    public string? ExtraJson { get; set; }
}

/// <summary>
/// 字典条目列表项。
/// </summary>
public sealed class DictionaryItemListItemDto
{
    /// <summary>
    /// 主键（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 分类 Id（Snowflake ID）。
    /// </summary>
    public long CategoryId { get; set; }

    /// <summary>
    /// 键。
    /// </summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>
    /// 值。
    /// </summary>
    public string ItemValue { get; set; } = string.Empty;

    /// <summary>
    /// 值-多语言 JSON。
    /// </summary>
    public string? ValueI18n { get; set; }

    /// <summary>
    /// 排序。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 上级条目 Id（层级型使用，Snowflake ID）。
    /// </summary>
    public long? ParentId { get; set; }
}

/// <summary>
/// 字典分类详情。
/// </summary>
public sealed class DictionaryCategoryDetailDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Category { get; set; }
    public string? SourceType { get; set; }
    public string? Description { get; set; }
    public string? ExtraJson { get; set; }
}

/// <summary>
/// 字典条目详情。
/// </summary>
public sealed class DictionaryItemDetailDto
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public string ItemValue { get; set; } = string.Empty;
    public string? ValueI18n { get; set; }
    public int Order { get; set; }
    public bool Enabled { get; set; }
    public long? ParentId { get; set; }
}

/// <summary>
/// 字典条目创建输入。
/// </summary>
public sealed class CreateDictionaryItemInput
{
    /// <summary>
    /// 分类 Id（Snowflake ID）。
    /// </summary>
    public long CategoryId { get; set; }

    /// <summary>
    /// 键。
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>
    /// 值。
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ItemValue { get; set; } = string.Empty;

    /// <summary>
    /// 值-多语言 JSON。
    /// </summary>
    public string? ValueI18n { get; set; }

    /// <summary>
    /// 上级条目 Id（层级型可选，Snowflake ID）。
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 扩展配置。
    /// </summary>
    public string? ExtraJson { get; set; }
}

/// <summary>
/// 字典条目更新输入。
/// </summary>
public sealed class UpdateDictionaryItemInput
{
    /// <summary>
    /// 键。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>
    /// 值。
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ItemValue { get; set; } = string.Empty;

    /// <summary>
    /// 值-多语言 JSON。
    /// </summary>
    public string? ValueI18n { get; set; }

    /// <summary>
    /// 排序。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 上级条目 Id（层级型可选，Snowflake ID）。
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 扩展配置。
    /// </summary>
    public string? ExtraJson { get; set; }
}

/// <summary>
/// 字典分类导出包（JSON 文件格式）。
/// </summary>
public sealed class DictionaryCategoryExportDto
{
    /// <summary>格式版本，便于后续兼容升级。</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>导出时间（本地时间）。</summary>
    public DateTime ExportedAt { get; set; }

    /// <summary>分类信息。</summary>
    public DictionaryCategoryExportCategoryDto Category { get; set; } = new();

    /// <summary>条目列表。</summary>
    public List<DictionaryCategoryExportItemDto> Items { get; set; } = new();
}

/// <summary>
/// 导出包中的分类信息（不含数据库 Id）。
/// </summary>
public sealed class DictionaryCategoryExportCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameI18n { get; set; }
    public string? Category { get; set; }
    public string? SourceType { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
    public string? DescriptionI18n { get; set; }
    public string? ExtraJson { get; set; }
    public string Module { get; set; } = "sys";
}

/// <summary>
/// 导出包中的条目信息（层级关系用 parentItemKey 表达，便于跨环境导入）。
/// </summary>
public sealed class DictionaryCategoryExportItemDto
{
    public string ItemKey { get; set; } = string.Empty;
    public string ItemValue { get; set; } = string.Empty;
    public string? ValueI18n { get; set; }
    public int Order { get; set; }
    public bool Enabled { get; set; } = true;
    public string? ParentItemKey { get; set; }
    public string? ExtraJson { get; set; }
}

/// <summary>
/// 字典分类导入结果。
/// </summary>
public sealed class DictionaryImportResultDto
{
    public long CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public bool CreatedCategory { get; set; }
    public int ItemsCreated { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsDeleted { get; set; }
}

