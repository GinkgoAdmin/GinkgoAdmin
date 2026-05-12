// 文件功能说明：
// 验证 ApplySecondLevelCache 的开关化挂载语义：
//   - Enabled=false → ConfigureExternalServices 不挂载 DataInfoCacheService
//   - Provider 非 Memory → 不挂载，输出 Warning（本测试只校验"未挂载"）
//   - 缺少 IMemoryCache → 不挂载
//   - Memory + IMemoryCache 可用 → DataInfoCacheService 被正确挂载到 MemoryCacheServiceAdapter

using Ginkgo.Infrastructure.Persistence.Extensions;
using Ginkgo.Infrastructure.Persistence.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace Ginkgo.Tests.Unit.Features;

public sealed class SecondLevelCacheApplyTests
{
    private static ConnectionConfig NewConfig() => new()
    {
        DbType = DbType.MySql,
        ConnectionString = "server=127.0.0.1;database=master;uid=u;pwd=p",
        IsAutoCloseConnection = true,
    };

    private static IServiceProvider SpWithMemoryCache()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        return services.BuildServiceProvider();
    }

    private static IServiceProvider SpWithoutMemoryCache()
        => new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void Disabled_DoesNotMount_DataInfoCacheService()
    {
        var cfg = NewConfig();
        var sp = SpWithMemoryCache();
        var opts = new SecondLevelCacheOptions { Enabled = false, Provider = "Memory" };

        ServiceCollectionExtensions.ApplySecondLevelCache(cfg, opts, logger: null, sp);

        Assert.Null(cfg.ConfigureExternalServices?.DataInfoCacheService);
    }

    [Fact]
    public void UnknownProvider_DoesNotMount()
    {
        var cfg = NewConfig();
        var sp = SpWithMemoryCache();
        var opts = new SecondLevelCacheOptions { Enabled = true, Provider = "Redis" };

        ServiceCollectionExtensions.ApplySecondLevelCache(cfg, opts, logger: null, sp);

        Assert.Null(cfg.ConfigureExternalServices?.DataInfoCacheService);
    }

    [Fact]
    public void Memory_WithoutIMemoryCacheRegistered_DoesNotMount()
    {
        var cfg = NewConfig();
        var sp = SpWithoutMemoryCache();
        var opts = new SecondLevelCacheOptions { Enabled = true, Provider = "Memory" };

        ServiceCollectionExtensions.ApplySecondLevelCache(cfg, opts, logger: null, sp);

        Assert.Null(cfg.ConfigureExternalServices?.DataInfoCacheService);
    }

    [Fact]
    public void Memory_HappyPath_MountsAdapter()
    {
        var cfg = NewConfig();
        var sp = SpWithMemoryCache();
        var opts = new SecondLevelCacheOptions { Enabled = true, Provider = "Memory", DefaultSeconds = 600 };

        ServiceCollectionExtensions.ApplySecondLevelCache(cfg, opts, logger: null, sp);

        Assert.NotNull(cfg.ConfigureExternalServices);
        Assert.NotNull(cfg.ConfigureExternalServices!.DataInfoCacheService);
        Assert.IsType<MemoryCacheServiceAdapter>(cfg.ConfigureExternalServices.DataInfoCacheService);
    }

    [Fact]
    public void Memory_CaseInsensitive_Provider()
    {
        var cfg = NewConfig();
        var sp = SpWithMemoryCache();
        var opts = new SecondLevelCacheOptions { Enabled = true, Provider = "memory" };

        ServiceCollectionExtensions.ApplySecondLevelCache(cfg, opts, logger: null, sp);

        Assert.NotNull(cfg.ConfigureExternalServices?.DataInfoCacheService);
    }
}
