// 文件功能说明：
// 以 JSONC（JSON with Comments）风格手工拼装 db.json 输出：
// - 每个配置项独占一行
// - 其上一行是对应的 // 中文注释
// - Database.Features.* 默认全部 Enabled=false，行为与《未引入 SqlSugar 高级能力》前一致；需要某项能力时再把对应 Enabled 改为 true。
//
// 设计要点：
// - 不使用 JsonSerializer，以精准控制排版与注释。
// - 产出的文本由 JsoncFileConfigurationProvider 解析（支持 // 注释），配置系统可正常读取。
// - 安装器只在《首次安装》时调用；已存在的 db.json 不会被本 Writer 覆写。

using System.Text;

namespace Ginkgo.Api.Install.Writers;

/// <summary>
/// 首次安装生成带中文注释的 db.json（JSONC 风格）。
/// </summary>
public static class DbJsonWriter
{
    /// <summary>
    /// 生成 db.json 的完整 JSONC 文本。
    /// </summary>
    /// <param name="jwtSigningKey">JWT 签名密钥（Base64）。</param>
    /// <param name="jwtIssuer">JWT 签发者。</param>
    /// <param name="jwtAudience">JWT 受众。</param>
    /// <param name="jwtExpiresMinutes">JWT 过期分钟数。</param>
    /// <param name="dbProvider">数据库驱动（MySql / SqlServer / PostgreSql 等）。</param>
    /// <param name="dbConnectionString">主连接串。</param>
    /// <returns>可直接写入文件的 JSONC 字符串（含 // 中文注释）。</returns>
    public static string Build(
        string jwtSigningKey,
        string jwtIssuer,
        string jwtAudience,
        int jwtExpiresMinutes,
        string dbProvider,
        string dbConnectionString)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("{");
        sb.AppendLine("  // 运行时数据库与 SqlSugar 高级能力配置，优先于 appsettings.json。");
        sb.AppendLine("  // 支持 // 与 /* */ 注释、尾随逗号；修改后下个请求即生效（reloadOnChange）。");
        sb.AppendLine();

        // ===== Jwt 段 =====
        sb.AppendLine("  \"Jwt\": {");
        sb.AppendLine("    // 签名密钥（Base64），生产环境必须保密。");
        sb.AppendLine($"    \"SigningKey\": {JsonString(jwtSigningKey)},");
        sb.AppendLine("    // 令牌签发者标识。");
        sb.AppendLine($"    \"Issuer\": {JsonString(jwtIssuer)},");
        sb.AppendLine("    // 令牌受众标识。");
        sb.AppendLine($"    \"Audience\": {JsonString(jwtAudience)},");
        sb.AppendLine("    // 令牌过期分钟数。");
        sb.AppendLine($"    \"ExpiresMinutes\": {jwtExpiresMinutes}");
        sb.AppendLine("  },");
        sb.AppendLine();

        // ===== Database 段 =====
        sb.AppendLine("  \"Database\": {");
        sb.AppendLine("    // 数据库驱动：开源版仅 MySql；商业版可选 SqlServer / PostgreSql / 后续方言。");
        sb.AppendLine($"    \"Provider\": {JsonString(dbProvider)},");
        sb.AppendLine("    // 启动时是否自动检查/创建数据库与表（仅开发环境建议开启）。");
        sb.AppendLine("    \"AutoCreate\": false,");
        sb.AppendLine();

        // ===== Database.Features 段 =====
        sb.AppendLine("    // SqlSugar 高级能力开关；按需启用。未设置或 Enabled=false 时完全不挂载，零开销。");
        sb.AppendLine("    // 说明文档：document/SqlSugar 性能优化建议.md");
        sb.AppendLine("    \"Features\": {");

        // ReadWriteSplit
        sb.AppendLine("      // 读写分离（主库写、从库读）。启用后需填写 Slaves。");
        sb.AppendLine("      \"ReadWriteSplit\": {");
        sb.AppendLine("        // 是否启用读写分离。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 从库列表，每项 { ConnectionString, HitRate }；HitRate 为相对权重。");
        sb.AppendLine("        \"Slaves\": []");
        sb.AppendLine("      },");

        // SecondLevelCache
        sb.AppendLine("      // 二级缓存（SqlSugar .WithCache() 的底层提供者）。");
        sb.AppendLine("      \"SecondLevelCache\": {");
        sb.AppendLine("        // 是否启用二级缓存。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 缓存提供者：Memory（默认、单节点） / Redis（需后续版本支持）。");
        sb.AppendLine("        \"Provider\": \"Memory\",");
        sb.AppendLine("        // 默认过期秒数，查询调用 .WithCache() 未指定时使用。");
        sb.AppendLine("        \"DefaultSeconds\": 300");
        sb.AppendLine("      },");

        // SplitTable
        sb.AppendLine("      // 自动分表（SplitTable）AOP 挂载开关。");
        sb.AppendLine("      \"SplitTable\": {");
        sb.AppendLine("        // 是否启用 SqlSugar 分表 AOP。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 分表策略：Year / Month / Day / Custom；业务侧需在实体上加 [SplitTable] 特性。");
        sb.AppendLine("        \"Strategy\": \"Month\"");
        sb.AppendLine("      },");

        // SaasMultiDb
        sb.AppendLine("      // SaaS 多库分库。本版本仅骨架，启用后输出告警日志；完整落地见 SaaS 多库设计.md。");
        sb.AppendLine("      \"SaasMultiDb\": {");
        sb.AppendLine("        // 是否声明启用 SaaS 多库（本版本不真实切库）。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 租户库连接列表，每项 { ConfigId, ConnectionString, Description }。");
        sb.AppendLine("        \"Connections\": []");
        sb.AppendLine("      },");

        // BulkOps（默认关闭）
        sb.AppendLine("      // 大数据写入能力（Fastest<T>.BulkCopy / BulkUpdate）。");
        sb.AppendLine("      \"BulkOps\": {");
        sb.AppendLine("        // 是否启用 BulkCopy 通路；false 时 IBulkInsertService 降级为 Insertable 逐批写入（行为与未引入本能力前一致）。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 默认批量大小；IBulkInsertService 未显式传入时使用。");
        sb.AppendLine("        \"DefaultBatchSize\": 5000");
        sb.AppendLine("      },");

        // SlowQuery（默认关闭）
        sb.AppendLine("      // 慢查询日志挂勾。默认关闭；Enabled=true 后超过阈值的 SQL 会输出 Warning 日志。");
        sb.AppendLine("      \"SlowQuery\": {");
        sb.AppendLine("        // 是否挂载 OnLogExecuted 回调；false 时完全不挂、零开销。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 慢查询阈值（毫秒）；Enabled=true 时超过该值输出 Warning 日志。");
        sb.AppendLine("        \"ThresholdMs\": 1000,");
        sb.AppendLine("        // 是否将慢 SQL 异步落入操作日志（后台 Channel，不阻塞 SQL 链路）。");
        sb.AppendLine("        \"WriteToOpLog\": false");
        sb.AppendLine("      },");

        // Reportable
        sb.AppendLine("      // BI 报表汇总查询入口（db.Reportable<T>(...)）。");
        sb.AppendLine("      \"Reportable\": {");
        sb.AppendLine("        // 是否启用；false 时调用 IReportableQueryService 会抛出提示异常。");
        sb.AppendLine("        \"Enabled\": false");
        sb.AppendLine("      },");

        // Concurrency
        sb.AppendLine("      // 多客户端并发执行包装器。");
        sb.AppendLine("      \"Concurrency\": {");
        sb.AppendLine("        // 是否启用；false 时 IConcurrentDbExecutor 退为串行。");
        sb.AppendLine("        \"Enabled\": false,");
        sb.AppendLine("        // 最大并发度；建议不超过连接池上限的 1/3。");
        sb.AppendLine("        \"MaxDegreeOfParallelism\": 4");
        sb.AppendLine("      }");

        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine();

        // ===== ConnectionStrings 段 =====
        sb.AppendLine("  \"ConnectionStrings\": {");
        sb.AppendLine("    // 主连接串，由方言驱动解析（如 MySql.Data、Microsoft.Data.SqlClient、Npgsql）。");
        sb.AppendLine($"    \"Default\": {JsonString(dbConnectionString)}");
        sb.AppendLine("  }");
        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// 安全的 JSON 字符串字面量序列化（处理反斜杠、引号、控制字符、非 ASCII 字符统一走 System.Text.Json 规则）。
    /// </summary>
    private static string JsonString(string? value)
    {
        if (value == null) return "null";
        // 借用 System.Text.Json 的编码规则以确保与 JSON 标准严格兼容。
        return System.Text.Json.JsonSerializer.Serialize(value);
    }
}
