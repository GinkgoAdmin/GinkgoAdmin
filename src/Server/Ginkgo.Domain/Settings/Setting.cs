using System.ComponentModel.DataAnnotations;
using SqlSugar;

namespace Ginkgo.Domain.Settings;

/// <summary>
/// 系统配置（键值对）。
/// 注意：此表的主键是 Key，不是 Id。
/// </summary>
[SugarTable("ginkgo_Sys_Settings", TableDescription = "系统配置（键值对）")]
[SugarIndex("IX_ginkgo_Sys_Settings_Key_Class", $"{nameof(Key)},{nameof(Class)}", OrderByType.Asc, true)]
public sealed class Setting : Ginkgo.Domain.Entity
{
    /// <summary>
    /// 覆盖基类的 Id，标记为非主键（此表主键是 Key）。
    /// </summary>
    [SugarColumn(IsPrimaryKey = false, ColumnName = "Id", ColumnDataType = "bigint", ColumnDescription = "实体Id（非主键）")]
    public new long Id { get; set; } = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId();

    /// <summary>
    /// 配置键（唯一，数据库主键）。
    /// 注意：SqlSugar 需要 public set 才能从数据库读取值。
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, Length = 256, IsNullable = false, ColumnDescription = "配置键（唯一）")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 配置值（字符串或 JSON）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "配置值（字符串/JSON）")]
    public string? Value { get; set; }

    /// <summary>
    /// 值类型：String/Number/Bool/Json。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "值类型：String/Number/Bool/Json")]
    public string? Type { get; set; }

    /// <summary>
    /// 描述（中文说明）。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "描述（中文说明）")]
    public string? Description { get; set; }

    /// <summary>
    /// 版本号。
    /// </summary>
    [SugarColumn(ColumnDescription = "版本号")]
    public int Version { get; set; }

    /// <summary>
    /// 并发控制版本（乐观并发）。
    /// 注意：SqlSugar 插入/更新时忽略该列（由数据库自动生成）。
    /// </summary>
    [Timestamp]
    [SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true, IsNullable = true, ColumnDescription = "并发控制版本（RowVersion）")]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// 更新时间（UTC）。
    /// </summary>
    [SugarColumn(ColumnDescription = "更新时间（UTC）")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 更新人。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "更新人")]
    public long? UpdatedBy { get; set; }

    /// <summary>
    /// 业务分类（sysconfig 字典）。
    /// </summary>
    [SugarColumn(ColumnName = "class", IsNullable = true, ColumnDescription = "业务分类")]
    public string? Class { get; set; }

    // ========= 领域行为 =========
    public static Setting Create(string key, string? value, string? type, string? description, string? @class, long? operatorId, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("配置键不能为空", nameof(key));
        var s = new Setting
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            Key = key.Trim(),
            Description = description?.Trim(),
            Class = string.IsNullOrWhiteSpace(@class) ? null : @class!.Trim(),
            Version = 1
        };
        s.SetValue(value, type, operatorId, nowUtc);
        return s;
    }

    public void SetValue(string? value, string? type, long? operatorId, DateTime? nowUtc = null)
    {
        var t = NormalizeType(type);
        // 类型校验（最小可行性）
        if ((t == "Number" || t == "Decimal") && !string.IsNullOrWhiteSpace(value)) _ = decimal.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
        if (t == "Integer" && !string.IsNullOrWhiteSpace(value)) _ = long.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
        if (t == "Bool" && !string.IsNullOrWhiteSpace(value)) _ = bool.Parse(value!);
        // Json 类型可以根据需要增加有效性校验（此处不强制）

        Value = value;
        Type = t;
        Touch(operatorId, nowUtc);
    }

    public void ChangeMeta(string? description, string? @class, long? operatorId, DateTime? nowUtc = null)
    {
        Description = description?.Trim();
        Class = string.IsNullOrWhiteSpace(@class) ? null : @class!.Trim();
        Touch(operatorId, nowUtc);
    }

    private static readonly HashSet<string> _knownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String", "Text", "Integer", "Number", "Decimal", "Bool",
        "Json", "RichText", "Password", "Color", "Url",
        "SingleImage", "MultiImage", "SingleFile", "MultiFile"
    };

    private static string? NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim();
        if (t.Equals("boolean", StringComparison.OrdinalIgnoreCase)) return "Bool";
        // 已知类型：规范化首字母大小写
        foreach (var known in _knownTypes)
        {
            if (t.Equals(known, StringComparison.OrdinalIgnoreCase)) return known;
        }
        // 未知类型原样保留
        return t;
    }

    private void Touch(long? operatorId, DateTime? nowUtc)
    {
        UpdatedBy = operatorId;
        UpdatedAt = nowUtc ?? DateTime.Now;
        Version = (Version <= 0 ? 1 : Version) + 1;
    }
}


