// PostgreSQL jsonb 列与 C# string 属性之间的 SqlSugar 读写桥接。

using System.Text.Json;
using NpgsqlTypes;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence;

/// <summary>
/// 仅对显式声明 <c>ColumnDataType = json/jsonb</c> 的 string 列做 PG jsonb 读写适配。
/// </summary>
internal static class PostgreSqlJsonbStringBridge
{
    /// <summary>实体列配置：MySQL json → PG jsonb，并标记 IsJson 供参数绑定识别。</summary>
    public static void ConfigureEntityColumn(EntityColumnInfo column)
    {
        if (!IsExplicitJsonColumn(column)) return;

        column.DataType = "jsonb";
        column.IsJson = true;
    }

    /// <summary>
    /// SQL 执行前：json 参数使用 JsonDocument 提交，避免 string 被 PG 存成 jsonb 字符串类型。
    /// </summary>
    public static KeyValuePair<string, SugarParameter[]> OnExecutingChangeSql(string sql, SugarParameter[] pars)
    {
        if (pars != null)
        {
            foreach (var p in pars)
            {
                if (!p.IsJson) continue;

                p.CustomDbType = NpgsqlDbType.Jsonb;
                if (p.Value is string text)
                {
                    p.Value = string.IsNullOrWhiteSpace(text)
                        ? null
                        : JsonDocument.Parse(text);
                }
            }
        }

        return new KeyValuePair<string, SugarParameter[]>(sql, pars);
    }

    /// <summary>查询后：将 jsonb 列还原/规范化为 JSON 对象文本。</summary>
    public static void OnDataExecuted(object? value, DataAfterModel entity)
    {
        var entityType = entity.Entity?.Type;
        if (entityType == null) return;

        foreach (var prop in entityType.GetProperties())
        {
            if (prop.PropertyType != typeof(string) || !IsJsonPropertyName(prop.Name)) continue;

            var current = entity.GetValue(prop.Name);
            var normalized = current switch
            {
                JsonDocument doc => NormalizeJsonText(doc.RootElement),
                JsonElement element => NormalizeJsonText(element),
                string s => TryNormalizeStoredJsonText(s),
                _ => null
            };

            if (normalized != null && !Equals(normalized, current))
                entity.SetValue(prop.Name, normalized);
        }
    }

    /// <summary>修正已误存为 jsonb 字符串类型的文本（去掉外层 JSON 字符串引号）。</summary>
    private static string? TryNormalizeStoredJsonText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var trimmed = text.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('[')) return text;

        if (!trimmed.StartsWith('"')) return text;
        try
        {
            var unwrapped = JsonSerializer.Deserialize<string>(trimmed);
            if (!string.IsNullOrWhiteSpace(unwrapped)
                && (unwrapped.TrimStart().StartsWith('{') || unwrapped.TrimStart().StartsWith('[')))
            {
                return unwrapped;
            }
        }
        catch
        {
            // 保持原值
        }

        return text;
    }

    private static string? NormalizeJsonText(JsonElement element)
    {
        // PG jsonb 若误存为 string 类型，读出来是 JsonValueKind.String，需再解析一层
        if (element.ValueKind == JsonValueKind.String)
        {
            var inner = element.GetString();
            if (string.IsNullOrWhiteSpace(inner)) return inner;
            try
            {
                using var doc = JsonDocument.Parse(inner);
                if (doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    return doc.RootElement.GetRawText();
            }
            catch
            {
                // 非 JSON 文本则按普通字符串返回
            }

            return inner;
        }

        return element.GetRawText();
    }

    private static bool IsJsonPropertyName(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return false;
        return propertyName.EndsWith("Json", StringComparison.OrdinalIgnoreCase)
               || propertyName.EndsWith("I18n", StringComparison.OrdinalIgnoreCase)
               || propertyName.EndsWith("Jsonb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitJsonColumn(EntityColumnInfo column)
    {
        var propertyType = column.UnderType ?? column.PropertyInfo?.PropertyType;
        if (propertyType != typeof(string)) return false;

        var dataType = column.DataType?.Trim();
        return !string.IsNullOrEmpty(dataType)
               && (dataType.Equals("json", StringComparison.OrdinalIgnoreCase)
                   || dataType.Equals("jsonb", StringComparison.OrdinalIgnoreCase));
    }
}
