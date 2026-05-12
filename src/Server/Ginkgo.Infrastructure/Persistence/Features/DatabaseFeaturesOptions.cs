// 文件功能说明：
// SqlSugar 高级能力的统一开关与配置 POCO。绑定到 db.json 的 Database.Features 节。
//
// 设计原则（详见 document/SqlSugar 性能优化建议.md §〇）：
// 1. 所有高级能力的入口集中在 db.json，不允许在 csproj / appsettings / 环境变量等位置另开开关。
// 2. 缺省、节点缺失、Enabled=false 时**完全不挂载**对应能力，保持零开销与现有行为。
// 3. BulkOps / SlowQuery 默认 Enabled=true，与现状（ImportController 已使用 BulkCopy；OnLogExecuted 已挂慢查询）一致；
//    其余能力默认 Enabled=false，按需手工启用。

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// SqlSugar 高级能力总开关与配置（绑定 db.json 的 <c>Database:Features</c> 节）。
/// </summary>
public sealed class DatabaseFeaturesOptions
{
    /// <summary>配置节路径（用于 <c>Configure&lt;T&gt;(GetSection(SectionName))</c>）。</summary>
    public const string SectionName = "Database:Features";

    /// <summary>读写分离（SqlSugar <c>SlaveConnectionConfigs</c>）。</summary>
    public ReadWriteSplitOptions ReadWriteSplit { get; set; } = new();

    /// <summary>二级缓存（SqlSugar <c>ICacheService</c> + <c>.WithCache()</c>）。</summary>
    public SecondLevelCacheOptions SecondLevelCache { get; set; } = new();

    /// <summary>自动分表（SqlSugar <c>SplitTable</c> AOP）。</summary>
    public SplitTableOptions SplitTable { get; set; } = new();

    /// <summary>SaaS 多库分库（按租户切 ConfigId）。本版本仅骨架。</summary>
    public SaasMultiDbOptions SaasMultiDb { get; set; } = new();

    /// <summary>大数据写入（SqlSugar <c>Fastest&lt;T&gt;()</c> / <c>BulkCopy</c> / <c>BulkUpdate</c>）。默认启用以兼容现状。</summary>
    public BulkOpsOptions BulkOps { get; set; } = new() { Enabled = true, DefaultBatchSize = 5000 };

    /// <summary>慢查询日志（基于 <c>OnLogExecuted</c>）。默认启用以兼容现状。</summary>
    public SlowQueryOptions SlowQuery { get; set; } = new() { Enabled = true, ThresholdMs = 1000, WriteToOpLog = false };

    /// <summary>BI 报表汇总查询入口（<c>db.Reportable&lt;T&gt;()</c>）。</summary>
    public ReportableOptions Reportable { get; set; } = new();

    /// <summary>多客户端并发执行（基于 <c>ISqlSugarClient.CopyNew()</c>）。</summary>
    public ConcurrencyOptions Concurrency { get; set; } = new();
}

/// <summary>读写分离配置：从库列表与权重。</summary>
public sealed class ReadWriteSplitOptions
{
    /// <summary>是否启用读写分离。<c>false</c>（默认）时不设置 <c>SlaveConnectionConfigs</c>。</summary>
    public bool Enabled { get; set; }

    /// <summary>从库列表，每项 <see cref="SlaveDatabaseOption"/>。</summary>
    public List<SlaveDatabaseOption> Slaves { get; set; } = new();
}

/// <summary>单个从库连接配置。</summary>
public sealed class SlaveDatabaseOption
{
    /// <summary>从库连接串。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>命中权重（相对值，数字越大被命中的概率越高）。建议非负，默认 10。</summary>
    public int HitRate { get; set; } = 10;
}

/// <summary>二级缓存配置。</summary>
public sealed class SecondLevelCacheOptions
{
    /// <summary>是否启用二级缓存。<c>false</c>（默认）时不注册 <c>DataInfoCacheService</c>。</summary>
    public bool Enabled { get; set; }

    /// <summary>缓存提供者：<c>Memory</c>（默认、单节点）或 <c>Redis</c>（后续轮次提供）。</summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>查询调用 <c>.WithCache()</c> 未指定秒数时的默认过期秒数。</summary>
    public int DefaultSeconds { get; set; } = 300;
}

/// <summary>自动分表配置。</summary>
public sealed class SplitTableOptions
{
    /// <summary>是否启用 SqlSugar SplitTable AOP。</summary>
    public bool Enabled { get; set; }

    /// <summary>默认分表策略名（仅文档语义；具体由实体上的 <c>[SplitTable]</c> 特性决定）。</summary>
    public string Strategy { get; set; } = "Month";
}

/// <summary>SaaS 多库分库配置（Stage C 已落地）。</summary>
public sealed class SaasMultiDbOptions
{
    /// <summary>是否启用 SaaS 多库。启用后通过 ITenantDbRouter.ChangeDatabase(configId) 切库。</summary>
    public bool Enabled { get; set; }

    /// <summary>租户库连接列表。</summary>
    public List<SaasDbConnectionOption> Connections { get; set; } = new();
}

/// <summary>SaaS 多库单条租户库连接。</summary>
public sealed class SaasDbConnectionOption
{
    /// <summary>SqlSugar ConfigId 标识。</summary>
    public string ConfigId { get; set; } = string.Empty;

    /// <summary>该 ConfigId 对应的数据库连接串。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>可选描述（用于运维标识）。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>大数据写入配置。默认启用。</summary>
public sealed class BulkOpsOptions
{
    /// <summary>是否启用 BulkCopy 通路。<c>false</c> 时 <c>IBulkInsertService</c> 退化为逐行 Insertable。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>未显式传参时的默认批量大小。</summary>
    public int DefaultBatchSize { get; set; } = 5000;
}

/// <summary>慢查询日志配置。默认启用以兼容现状。</summary>
public sealed class SlowQueryOptions
{
    /// <summary>是否挂载慢查询回调。<c>false</c> 时不挂 OnLogExecuted、零开销。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>慢查询阈值（毫秒），超过该值输出 Warning 日志。</summary>
    public int ThresholdMs { get; set; } = 1000;

    /// <summary>是否将慢 SQL 异步落入操作日志（OpLog）。后台 Channel 异步处理，不阻塞 SQL 链路。</summary>
    public bool WriteToOpLog { get; set; }
}

/// <summary>BI 报表查询入口配置。</summary>
public sealed class ReportableOptions
{
    /// <summary>是否启用 Reportable 入口。<c>false</c> 时调用 <c>IReportableQueryService</c> 会抛出提示异常。</summary>
    public bool Enabled { get; set; }
}

/// <summary>多客户端并发执行配置。</summary>
public sealed class ConcurrencyOptions
{
    /// <summary>是否启用并发执行包装器。<c>false</c> 时退化为串行。</summary>
    public bool Enabled { get; set; }

    /// <summary>最大并发度。建议不超过连接池上限的 1/3。</summary>
    public int MaxDegreeOfParallelism { get; set; } = 4;
}
