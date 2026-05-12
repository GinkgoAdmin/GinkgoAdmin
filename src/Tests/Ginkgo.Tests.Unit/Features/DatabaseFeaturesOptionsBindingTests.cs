// 文件功能说明：
// 验证 DatabaseFeaturesOptions 绑定的四档行为：
//   1. 完全未配置 → 使用默认值（BulkOps/SlowQuery 默认 true，其余 false）
//   2. 全部关闭   → 全部 Enabled=false
//   3. 部分启用   → 启用项正确读取、未启用项保持默认
//   4. 全部启用   → 所有字段正确读取
//
// 这保证了"开关化设计"的核心承诺：未配置等价于"全关"（但兼容现状的两个能力默认开）。

using Ginkgo.Infrastructure.Persistence.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ginkgo.Tests.Unit.Features;

public sealed class DatabaseFeaturesOptionsBindingTests
{
    private static DatabaseFeaturesOptions BindFromInMemory(Dictionary<string, string?> data)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var services = new ServiceCollection();
        services.Configure<DatabaseFeaturesOptions>(cfg.GetSection(DatabaseFeaturesOptions.SectionName));
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<DatabaseFeaturesOptions>>().Value;
    }

    [Fact]
    public void NoConfig_UsesDefaults()
    {
        var opts = BindFromInMemory(new Dictionary<string, string?>());

        // 默认启用（兼容现状）
        Assert.True(opts.BulkOps.Enabled);
        Assert.Equal(5000, opts.BulkOps.DefaultBatchSize);
        Assert.True(opts.SlowQuery.Enabled);
        Assert.Equal(1000, opts.SlowQuery.ThresholdMs);
        Assert.False(opts.SlowQuery.WriteToOpLog);

        // 默认关闭
        Assert.False(opts.ReadWriteSplit.Enabled);
        Assert.False(opts.SecondLevelCache.Enabled);
        Assert.False(opts.SplitTable.Enabled);
        Assert.False(opts.SaasMultiDb.Enabled);
        Assert.False(opts.Reportable.Enabled);
        Assert.False(opts.Concurrency.Enabled);
    }

    [Fact]
    public void AllDisabled_ExplicitFalse_Works()
    {
        var data = new Dictionary<string, string?>
        {
            ["Database:Features:BulkOps:Enabled"] = "false",
            ["Database:Features:SlowQuery:Enabled"] = "false",
            ["Database:Features:ReadWriteSplit:Enabled"] = "false",
            ["Database:Features:SecondLevelCache:Enabled"] = "false",
            ["Database:Features:SplitTable:Enabled"] = "false",
            ["Database:Features:SaasMultiDb:Enabled"] = "false",
            ["Database:Features:Reportable:Enabled"] = "false",
            ["Database:Features:Concurrency:Enabled"] = "false",
        };
        var opts = BindFromInMemory(data);

        Assert.False(opts.BulkOps.Enabled);
        Assert.False(opts.SlowQuery.Enabled);
        Assert.False(opts.ReadWriteSplit.Enabled);
        Assert.False(opts.SecondLevelCache.Enabled);
        Assert.False(opts.SplitTable.Enabled);
        Assert.False(opts.SaasMultiDb.Enabled);
        Assert.False(opts.Reportable.Enabled);
        Assert.False(opts.Concurrency.Enabled);
    }

    [Fact]
    public void PartialEnable_KeepsOtherDefaults()
    {
        // 只启用 ReadWriteSplit；其他应保持默认
        var data = new Dictionary<string, string?>
        {
            ["Database:Features:ReadWriteSplit:Enabled"] = "true",
            ["Database:Features:ReadWriteSplit:Slaves:0:ConnectionString"] = "cs1",
            ["Database:Features:ReadWriteSplit:Slaves:0:HitRate"] = "7",
        };
        var opts = BindFromInMemory(data);

        Assert.True(opts.ReadWriteSplit.Enabled);
        Assert.Single(opts.ReadWriteSplit.Slaves);
        Assert.Equal("cs1", opts.ReadWriteSplit.Slaves[0].ConnectionString);
        Assert.Equal(7, opts.ReadWriteSplit.Slaves[0].HitRate);

        // 兼容默认保留
        Assert.True(opts.BulkOps.Enabled);
        Assert.True(opts.SlowQuery.Enabled);
        Assert.False(opts.SecondLevelCache.Enabled);
    }

    [Fact]
    public void AllEnabled_WithCustomValues_Works()
    {
        var data = new Dictionary<string, string?>
        {
            ["Database:Features:BulkOps:Enabled"] = "true",
            ["Database:Features:BulkOps:DefaultBatchSize"] = "10000",
            ["Database:Features:SlowQuery:Enabled"] = "true",
            ["Database:Features:SlowQuery:ThresholdMs"] = "500",
            ["Database:Features:SlowQuery:WriteToOpLog"] = "true",
            ["Database:Features:ReadWriteSplit:Enabled"] = "true",
            ["Database:Features:SecondLevelCache:Enabled"] = "true",
            ["Database:Features:SecondLevelCache:Provider"] = "Redis",
            ["Database:Features:SecondLevelCache:DefaultSeconds"] = "600",
            ["Database:Features:SplitTable:Enabled"] = "true",
            ["Database:Features:SplitTable:Strategy"] = "Day",
            ["Database:Features:SaasMultiDb:Enabled"] = "true",
            ["Database:Features:Reportable:Enabled"] = "true",
            ["Database:Features:Concurrency:Enabled"] = "true",
            ["Database:Features:Concurrency:MaxDegreeOfParallelism"] = "8",
        };
        var opts = BindFromInMemory(data);

        Assert.Equal(10000, opts.BulkOps.DefaultBatchSize);
        Assert.Equal(500, opts.SlowQuery.ThresholdMs);
        Assert.True(opts.SlowQuery.WriteToOpLog);
        Assert.True(opts.ReadWriteSplit.Enabled);
        Assert.Equal("Redis", opts.SecondLevelCache.Provider);
        Assert.Equal(600, opts.SecondLevelCache.DefaultSeconds);
        Assert.Equal("Day", opts.SplitTable.Strategy);
        Assert.True(opts.SaasMultiDb.Enabled);
        Assert.True(opts.Reportable.Enabled);
        Assert.Equal(8, opts.Concurrency.MaxDegreeOfParallelism);
    }
}
