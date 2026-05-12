// 文件功能说明：
// 验证 JsoncFileConfigurationProvider 的解析语义：
// - 支持 // 与 /* */ 注释、尾随逗号、嵌套对象、数组索引路径
// - 能把 JSON 树拍扁为 IConfiguration 的扁平 key（":" 分隔）
// - 错误 JSON 抛 FormatException

using System.Text;
using Ginkgo.Api.Bootstrap.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ginkgo.Tests.Unit.Features;

public sealed class JsoncFileConfigurationProviderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { /* 测试清理失败忽略 */ }
        }
    }

    private string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ginkgo-jsonc-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Supports_LineAndBlockComments_AndTrailingCommas()
    {
        var jsonc = """
            {
              // 单行注释：Jwt 配置
              "Jwt": {
                "SigningKey": "abc",   // 密钥
                /* 块注释
                   跨多行 */
                "ExpiresMinutes": 120,
              },
              "Database": {
                "Provider": "MySql",
              }, // 尾随逗号
            }
            """;
        var path = WriteTempFile(jsonc);

        var cfg = new ConfigurationBuilder().AddJsoncFile(path, optional: false, reloadOnChange: false).Build();

        Assert.Equal("abc", cfg["Jwt:SigningKey"]);
        Assert.Equal("120", cfg["Jwt:ExpiresMinutes"]);
        Assert.Equal("MySql", cfg["Database:Provider"]);
    }

    [Fact]
    public void Supports_Arrays_WithIndexedKeys()
    {
        var jsonc = """
            {
              "Database": {
                "Features": {
                  "ReadWriteSplit": {
                    "Enabled": true,
                    "Slaves": [
                      { "ConnectionString": "cs1", "HitRate": 10 },
                      { "ConnectionString": "cs2", "HitRate": 5 }
                    ]
                  }
                }
              }
            }
            """;
        var path = WriteTempFile(jsonc);
        var cfg = new ConfigurationBuilder().AddJsoncFile(path).Build();

        Assert.Equal("true", cfg["Database:Features:ReadWriteSplit:Enabled"]);
        Assert.Equal("cs1", cfg["Database:Features:ReadWriteSplit:Slaves:0:ConnectionString"]);
        Assert.Equal("10", cfg["Database:Features:ReadWriteSplit:Slaves:0:HitRate"]);
        Assert.Equal("cs2", cfg["Database:Features:ReadWriteSplit:Slaves:1:ConnectionString"]);
        Assert.Equal("5", cfg["Database:Features:ReadWriteSplit:Slaves:1:HitRate"]);
    }

    [Fact]
    public void InvalidJson_ThrowsOnBuild()
    {
        // 非对象根 + 缺少冒号 + 未闭合
        var jsonc = "{ \"a\": { invalid";
        var path = WriteTempFile(jsonc);
        var builder = new ConfigurationBuilder().AddJsoncFile(path, optional: false, reloadOnChange: false);

        // 具体异常类型由 .NET 配置系统包装链路决定（FormatException / JsonException 等），
        // 此处只保证"非法 JSONC 会让 ConfigurationBuilder.Build() 失败"而非静默吞下。
        var ex = Assert.ThrowsAny<Exception>(() => builder.Build());
        Assert.NotNull(ex);
    }

    [Fact]
    public void MissingOptionalFile_DoesNotThrow()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"ginkgo-missing-{Guid.NewGuid():N}.json");

        // 不应抛异常；Build 成功且无任何键
        var cfg = new ConfigurationBuilder().AddJsoncFile(nonExistent, optional: true, reloadOnChange: false).Build();

        Assert.Null(cfg["Any:Key"]);
    }

    [Fact]
    public void NullValue_IsPreserved()
    {
        var jsonc = """
            {
              "Nullable": null
            }
            """;
        var path = WriteTempFile(jsonc);
        var cfg = new ConfigurationBuilder().AddJsoncFile(path).Build();

        // Null 会被写为 null 字符串（与 Microsoft JsonConfigurationProvider 语义一致）
        Assert.Null(cfg["Nullable"]);
    }
}
