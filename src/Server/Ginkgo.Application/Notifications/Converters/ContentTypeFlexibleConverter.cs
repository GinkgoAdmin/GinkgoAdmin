using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ginkgo.Application.Notifications.Converters
{
    /// <summary>
    /// 兼容旧/新枚举与字符串：
    /// - 数字 0(旧:Text)->1(新:Text)
    /// - 数字 1(旧:Html)->2(新:Html)
    /// - 数字 2(Markdown/Html)->2
    /// - 字符串 "text"->1, "html"->2 (大小写不敏感)
    /// - 其余数字/字符串将抛出格式异常
    /// </summary>
    public sealed class ContentTypeFlexibleConverter : JsonConverter<byte>
    {
        public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString()?.Trim().ToLowerInvariant();
                return s switch
                {
                    "text" or "plain" or "txt" => (byte)1,
                    "html" or "rich" => (byte)2,
                    _ => throw new JsonException($"不支持的内容类型: {s}")
                };
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (!reader.TryGetByte(out var n))
                    throw new JsonException("contentType 必须是 0/1/2 或 'text'/'html'");
                // 旧到新映射
                return n switch
                {
                    0 => (byte)1, // 旧Text
                    1 => (byte)2, // 旧Html
                    2 => (byte)2, // 旧Markdown/Html -> 统一按 Html 处理
                    3 => (byte)2, // 容错：将更高值也收敛到 Html
                    _ => throw new JsonException($"不支持的内容类型值: {n}")
                };
            }
            throw new JsonException("contentType 需要是字符串或数字");
        }

        public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options)
        {
            // 输出仍用数字，保持与现有 API 行为一致
            writer.WriteNumberValue(value);
        }
    }
}

