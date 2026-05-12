// 文件功能说明：
// 验证 DbJsonWriter 生成的 db.json JSONC：
//   - 包含预期节（Jwt / Database / Database.Features.* / ConnectionStrings）
//   - 每个关键配置独占一行，且上一行是 // 注释
//   - 能被 JsoncFileConfigurationProvider 解析、ConfigurationBuilder 下读出预期键值

using System.Text;
using Ginkgo.Api.Bootstrap.Configuration;
using Ginkgo.Api.Install.Writers;
using Microsoft.Extensions.Configuration;

namespace Ginkgo.Tests.Unit.Features;

public sealed class DbJsonWriterTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { /* ignore */ }
        }
    }

    private string Write(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ginkgo-dbjson-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Build_ProducesJsoncWith_ExpectedSectionsAndComments()
    {
        var text = DbJsonWriter.Build(
            jwtSigningKey: "K",
            jwtIssuer: "ginkgo",
            jwtAudience: "ginkgo-clients",
            jwtExpiresMinutes: 120,
            dbProvider: "MySql",
            dbConnectionString: "server=127.0.0.1;database=pgzx;uid=x;pwd=x");

        // 必含节点
        Assert.Contains("\"Jwt\"", text);
        Assert.Contains("\"Database\"", text);
        Assert.Contains("\"Features\"", text);
        Assert.Contains("\"ReadWriteSplit\"", text);
        Assert.Contains("\"SecondLevelCache\"", text);
        Assert.Contains("\"SplitTable\"", text);
        Assert.Contains("\"SaasMultiDb\"", text);
        Assert.Contains("\"BulkOps\"", text);
        Assert.Contains("\"SlowQuery\"", text);
        Assert.Contains("\"Reportable\"", text);
        Assert.Contains("\"Concurrency\"", text);
        Assert.Contains("\"ConnectionStrings\"", text);

        // 必含 // 注释
        Assert.Contains("// 运行时数据库", text);
        Assert.Contains("// 是否启用读写分离", text);
        Assert.Contains("// 默认批量大小", text);
        Assert.Contains("// 慢查询阈值", text);
    }

    [Fact]
    public void Build_EveryKeyConfig_IsOnItsOwnLine_WithLeadingCommentLine()
    {
        var text = DbJsonWriter.Build("K", "ginkgo", "ginkgo-clients", 120, "MySql", "cs");
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // 找出若干关键配置行，确保其前面紧邻的一行是一个 // 注释行（允许空白缩进）。
        AssertHasLeadingCommentLine(lines, "\"Provider\":");
        AssertHasLeadingCommentLine(lines, "\"AutoCreate\":");
        AssertHasLeadingCommentLine(lines, "\"Enabled\":", limitFirstOccurrence: 1); // 至少第一个 Enabled 前面有注释
        AssertHasLeadingCommentLine(lines, "\"DefaultBatchSize\":");
        AssertHasLeadingCommentLine(lines, "\"ThresholdMs\":");
        AssertHasLeadingCommentLine(lines, "\"WriteToOpLog\":");
        AssertHasLeadingCommentLine(lines, "\"Default\":");
    }

    [Fact]
    public void Build_ProducesText_Parsable_ByJsoncProvider()
    {
        var text = DbJsonWriter.Build(
            jwtSigningKey: "signing-key-value",
            jwtIssuer: "ginkgo",
            jwtAudience: "ginkgo-clients",
            jwtExpiresMinutes: 120,
            dbProvider: "MySql",
            dbConnectionString: "server=host;database=db;uid=u;pwd=p");

        var path = Write(text);
        var cfg = new ConfigurationBuilder().AddJsoncFile(path).Build();

        // JWT
        Assert.Equal("signing-key-value", cfg["Jwt:SigningKey"]);
        Assert.Equal("ginkgo", cfg["Jwt:Issuer"]);
        Assert.Equal("120", cfg["Jwt:ExpiresMinutes"]);

        // Database
        Assert.Equal("MySql", cfg["Database:Provider"]);
        Assert.Equal("False", cfg["Database:AutoCreate"], ignoreCase: true);

        // Features：安装模板默认全部关闭（含 BulkOps/SlowQuery，行为与未引入前一致）。
        Assert.Equal("False", cfg["Database:Features:BulkOps:Enabled"], ignoreCase: true);
        Assert.Equal("5000", cfg["Database:Features:BulkOps:DefaultBatchSize"]);
        Assert.Equal("False", cfg["Database:Features:SlowQuery:Enabled"], ignoreCase: true);
        Assert.Equal("1000", cfg["Database:Features:SlowQuery:ThresholdMs"]);
        Assert.Equal("False", cfg["Database:Features:SlowQuery:WriteToOpLog"], ignoreCase: true);

        Assert.Equal("False", cfg["Database:Features:ReadWriteSplit:Enabled"], ignoreCase: true);
        Assert.Equal("False", cfg["Database:Features:SecondLevelCache:Enabled"], ignoreCase: true);
        Assert.Equal("Memory", cfg["Database:Features:SecondLevelCache:Provider"]);
        Assert.Equal("300", cfg["Database:Features:SecondLevelCache:DefaultSeconds"]);
        Assert.Equal("False", cfg["Database:Features:SplitTable:Enabled"], ignoreCase: true);
        Assert.Equal("Month", cfg["Database:Features:SplitTable:Strategy"]);
        Assert.Equal("False", cfg["Database:Features:SaasMultiDb:Enabled"], ignoreCase: true);
        Assert.Equal("False", cfg["Database:Features:Reportable:Enabled"], ignoreCase: true);
        Assert.Equal("False", cfg["Database:Features:Concurrency:Enabled"], ignoreCase: true);
        Assert.Equal("4", cfg["Database:Features:Concurrency:MaxDegreeOfParallelism"]);

        // ConnectionStrings
        Assert.Equal("server=host;database=db;uid=u;pwd=p", cfg["ConnectionStrings:Default"]);
    }

    private static void AssertHasLeadingCommentLine(string[] lines, string needle, int limitFirstOccurrence = 0)
    {
        var found = 0;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Contains(needle))
            {
                var prev = lines[i - 1].TrimStart();
                Assert.True(
                    prev.StartsWith("//"),
                    $"行 {i + 1} 包含 '{needle}'，但上一行不是 // 注释：'{lines[i - 1]}'");
                found++;
                if (limitFirstOccurrence > 0 && found >= limitFirstOccurrence) return;
            }
        }
        Assert.True(found > 0, $"未在文本中找到 '{needle}'。");
    }
}
