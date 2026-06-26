// 文件功能说明：
// 定义数据字典条目实体。

using SqlSugar;

namespace Ginkgo.Domain.Dictionaries;

/// <summary>
/// 字典条目实体。
/// </summary>
[SugarTable("ginkgo_Sys_DictionaryItem", TableDescription = "数据字典项表（支持层级、按分类唯一键，含启用与排序）")]
[SugarIndex("IX_DictItem_Category_Order", $"{nameof(CategoryId)},{nameof(Order)}", OrderByType.Asc)]
[SugarIndex("UX_DictItem_Category_Key", $"{nameof(CategoryId)},{nameof(ItemKey)}", OrderByType.Asc, true)]
[SugarIndex("IX_DictItem_Module", nameof(Module), OrderByType.Asc)]
public sealed class DictionaryItem : AuditableEntity
{
    /// <summary>
    /// 所属模块标识：sys = 主框架系统级，其他为插件 module.json 中的 id。
    /// 默认随所属字典分类，便于插件卸载时按模块批量清理。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false, ColumnDescription = "所属模块标识（sys=系统级，其他为插件ModuleId）", DefaultValue = "sys")]
    public string Module { get; set; } = "sys";

    /// <summary>
    /// 上级条目 Id（用于层级型字典）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "上级条目Id（可空）")]
    public long? ParentId { get; set; }
    /// <summary>
    /// 所属分类 Id。
    /// </summary>
    [SugarColumn(ColumnName = "DictId", ColumnDescription = "所属分类Id")]
    public long CategoryId { get; set; }

    /// <summary>
    /// 条目键。
    /// </summary>
    [SugarColumn(ColumnName = "Code", Length = 100, IsNullable = false, ColumnDescription = "条目键（同一分类唯一）")]
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>
    /// 条目值。
    /// </summary>
    [SugarColumn(ColumnName = "Value", Length = 200, IsNullable = false, ColumnDescription = "条目值（显示/配置值）")]
    public string ItemValue { get; set; } = string.Empty;

    /// <summary>
    /// 条目值-多语言 JSON
    /// </summary>
    [SugarColumn(ColumnName = "ValueI18n", ColumnDataType = "json", IsNullable = true, ColumnDescription = "条目值-多语言")]
    public string? ValueI18n { get; set; }

    /// <summary>
    /// 排序号。
    /// </summary>
    [SugarColumn(ColumnName = "SortOrder", ColumnDescription = "排序号（同分类升序）")]
    public int Order { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    [SugarColumn(ColumnName = "IsActive", ColumnDescription = "是否启用")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 扩展数据（不同类型的条目的差异化配置）。
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true, ColumnDescription = "扩展JSON")]
    public string? ExtraJson { get; set; }

    // ===== 领域行为 =====
    public static DictionaryItem Create(long categoryId, string key, string value, long? parentId = null)
    {
        if (categoryId == 0) throw new ArgumentException("categoryId 不能为空", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("条目键不能为空", nameof(key));
        return new DictionaryItem
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            CategoryId = categoryId,
            ItemKey = key.Trim(),
            ItemValue = value?.Trim() ?? string.Empty,
            ParentId = parentId,
            Order = 0,
            Enabled = true
        };
    }

    public void UpdateValue(string value) => ItemValue = value?.Trim() ?? string.Empty;
    public void RenameKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("条目键不能为空", nameof(key));
        ItemKey = key.Trim();
    }
    public void MoveTo(long? newParentId) => ParentId = newParentId;
    public void SetOrder(int order) => Order = order < 0 ? 0 : order;
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
}






