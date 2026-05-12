// 文件功能说明：
// JSONC（JSON with Comments）配置源与 Provider 实现。
// 让 db.json 等运行时配置文件支持 // 单行注释、/* */ 块注释与尾随逗号，
// 同时保留 .NET 内置 FileConfigurationProvider 的 reloadOnChange 热更新能力。
//
// 设计要点：
// - 继承 FileConfigurationSource / FileConfigurationProvider，复用框架文件监视机制。
// - 解析使用 System.Text.Json 自带的 JsonCommentHandling.Skip，零额外依赖。
// - 把 JsonDocument 树拍扁为 IConfiguration 字典，与 Microsoft.Extensions.Configuration.Json 行为对齐。

using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Ginkgo.Api.Bootstrap.Configuration;

/// <summary>
/// JSONC 配置源：用于 <see cref="JsoncConfigurationExtensions.AddJsoncFile"/> 注册。
/// </summary>
public sealed class JsoncFileConfigurationSource : FileConfigurationSource
{
    /// <inheritdoc />
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new JsoncFileConfigurationProvider(this);
    }
}

/// <summary>
/// JSONC 配置 Provider：在解析阶段跳过 // 与 /* */ 注释、容忍尾随逗号，
/// 把 JSON 树拍扁为 IConfiguration 字典。
/// </summary>
public sealed class JsoncFileConfigurationProvider : FileConfigurationProvider
{
    public JsoncFileConfigurationProvider(JsoncFileConfigurationSource source)
        : base(source)
    {
    }

    /// <inheritdoc />
    public override void Load(Stream stream)
    {
        try
        {
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            // CommentHandling.Skip 让 // 与 /* */ 注释直接被解析器忽略，无需预处理；
            // AllowTrailingCommas 容忍人工编辑常见的尾随逗号，提升运维体验。
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("JSONC 配置根节点必须是对象（{}）。");
            }

            VisitElement(doc.RootElement, prefix: string.Empty, data);
            Data = data;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"无法解析 JSONC 文件：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 递归把 JSON 树拍扁为 IConfiguration 兼容的扁平字典（key 用 ":" 分隔）。
    /// 对齐 .NET 内置 JsonConfigurationFileParser 的可见行为。
    /// </summary>
    private static void VisitElement(JsonElement element, string prefix, IDictionary<string, string?> data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    VisitElement(prop.Value, Combine(prefix, prop.Name), data);
                }
                break;
            case JsonValueKind.Array:
                var idx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    VisitElement(item, Combine(prefix, idx.ToString(System.Globalization.CultureInfo.InvariantCulture)), data);
                    idx++;
                }
                break;
            case JsonValueKind.String:
                data[prefix] = element.GetString();
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                data[prefix] = element.GetRawText();
                break;
            case JsonValueKind.Null:
                data[prefix] = null;
                break;
            case JsonValueKind.Undefined:
                // JSON 中不会出现 Undefined；兜底跳过。
                break;
        }
    }

    private static string Combine(string prefix, string segment) =>
        string.IsNullOrEmpty(prefix) ? segment : prefix + ConfigurationPath.KeyDelimiter + segment;
}

/// <summary>
/// JSONC 配置扩展方法。
/// </summary>
public static class JsoncConfigurationExtensions
{
    /// <summary>
    /// 向配置构建器添加一个支持 // 注释、/* */ 注释与尾随逗号的 JSONC 文件源。
    /// 行为与 <c>AddJsonFile</c> 一致，但解析阶段会跳过注释；保留 reloadOnChange 热更新能力。
    /// </summary>
    /// <param name="builder">配置构建器。</param>
    /// <param name="path">JSONC 文件路径（绝对或相对工作目录）。</param>
    /// <param name="optional">文件不存在时是否允许通过（true 表示允许）。</param>
    /// <param name="reloadOnChange">文件内容变化时是否自动重载配置。</param>
    public static IConfigurationBuilder AddJsoncFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        bool reloadOnChange = false)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("文件路径不能为空。", nameof(path));

        return builder.Add<JsoncFileConfigurationSource>(s =>
        {
            s.FileProvider = null; // ResolveFileProvider 会按 path 自动解析为合适的 PhysicalFileProvider
            s.Path = path;
            s.Optional = optional;
            s.ReloadOnChange = reloadOnChange;
            s.ResolveFileProvider();
        });
    }
}
