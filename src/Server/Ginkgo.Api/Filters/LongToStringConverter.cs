using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ginkgo.Api.Filters;

/// <summary>
/// 将 long 类型序列化为字符串，避免 JavaScript 精度丢失问题。
/// Snowflake ID 是 64 位整数，超过 JavaScript Number.MAX_SAFE_INTEGER (2^53-1)。
/// </summary>
public class LongToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (long.TryParse(str, out var value))
                return value;
        }
        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// 将 long? 类型序列化为字符串。
/// </summary>
public class NullableLongToStringConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str))
                return null;
            if (long.TryParse(str, out var value))
                return value;
        }
        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// 将 DateTime 类型序列化为 UTC 字符串，解决时区问题。
/// MySQL 读回的 DateTime 默认 Kind 为 Unspecified，序列化时若无 'Z' 后缀，
/// 浏览器会将其解析为本地时间而非 UTC。此转换器强制所有 DateTime 值在序列化时视为 UTC，
/// 并添加 'Z' 后缀，确保浏览器正确转换为本地时间（+8 小时）。
/// </summary>
public class DateTimeToUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (DateTime.TryParse(str, out var value))
            {
                // Ensure the DateTime is treated as UTC
                return value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
            }
        }
        // Fallback for other token types or parsing failures, let default deserialization handle it
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Convert to UTC and format with 'Z' suffix
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}

/// <summary>
/// 将 Nullable DateTime 类型序列化为 UTC 字符串，解决时区问题。
/// MySQL 读回的 DateTime 默认 Kind 为 Unspecified，序列化时若无 'Z' 后缀，
/// 浏览器会将其解析为本地时间而非 UTC。此转换器强制所有 DateTime 值在序列化时视为 UTC，
/// 并添加 'Z' 后缀，确保浏览器正确转换为本地时间（+8 小时）。
/// </summary>
public class NullableDateTimeToUtcConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str))
                return null;
            if (DateTime.TryParse(str, out var value))
            {
                // Ensure the DateTime is treated as UTC
                return value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
            }
        }
        // Fallback for other token types or parsing failures, let default deserialization handle it
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            // Convert to UTC and format with 'Z' suffix
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
