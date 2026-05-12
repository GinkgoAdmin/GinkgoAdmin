// 文件功能说明：
// 验证 ApplyReadWriteSplit 的开关化挂载语义：
//   - Enabled=false 或 Slaves 空 → ConnectionConfig.SlaveConnectionConfigs 不被设置（保持单主库行为）
//   - Enabled=true + 部分配置无效 → 只挂载有效项
//   - Enabled=true + 全部无效 → 不挂载，输出 Warning
//   - 正常路径 → HitRate 被正确传递，<=0 时使用默认 10

using Ginkgo.Infrastructure.Persistence.Extensions;
using Ginkgo.Infrastructure.Persistence.Features;
using SqlSugar;

namespace Ginkgo.Tests.Unit.Features;

public sealed class ReadWriteSplitApplyTests
{
    private static ConnectionConfig NewConfig() => new()
    {
        DbType = DbType.MySql,
        ConnectionString = "server=127.0.0.1;database=master;uid=u;pwd=p",
        IsAutoCloseConnection = true,
    };

    [Fact]
    public void Disabled_DoesNotSet_SlaveConnectionConfigs()
    {
        var config = NewConfig();
        var options = new ReadWriteSplitOptions { Enabled = false };
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "cs1", HitRate = 10 });

        ServiceCollectionExtensions.ApplyReadWriteSplit(config, options, logger: null);

        Assert.Null(config.SlaveConnectionConfigs);
    }

    [Fact]
    public void EnabledButEmptySlaves_DoesNotSet_SlaveConnectionConfigs()
    {
        var config = NewConfig();
        var options = new ReadWriteSplitOptions { Enabled = true }; // 默认 Slaves 列表为空

        ServiceCollectionExtensions.ApplyReadWriteSplit(config, options, logger: null);

        Assert.Null(config.SlaveConnectionConfigs);
    }

    [Fact]
    public void EnabledAllInvalid_DoesNotSet_SlaveConnectionConfigs()
    {
        var config = NewConfig();
        var options = new ReadWriteSplitOptions { Enabled = true };
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "", HitRate = 10 });
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "   ", HitRate = 5 });

        ServiceCollectionExtensions.ApplyReadWriteSplit(config, options, logger: null);

        Assert.Null(config.SlaveConnectionConfigs);
    }

    [Fact]
    public void EnabledWithValidSlaves_PopulatesSlaveConnectionConfigs()
    {
        var config = NewConfig();
        var options = new ReadWriteSplitOptions { Enabled = true };
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "cs1", HitRate = 7 });
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "cs2", HitRate = 3 });

        ServiceCollectionExtensions.ApplyReadWriteSplit(config, options, logger: null);

        Assert.NotNull(config.SlaveConnectionConfigs);
        Assert.Equal(2, config.SlaveConnectionConfigs.Count);
        Assert.Equal("cs1", config.SlaveConnectionConfigs[0].ConnectionString);
        Assert.Equal(7, config.SlaveConnectionConfigs[0].HitRate);
        Assert.Equal("cs2", config.SlaveConnectionConfigs[1].ConnectionString);
        Assert.Equal(3, config.SlaveConnectionConfigs[1].HitRate);
    }

    [Fact]
    public void NonPositiveHitRate_FallsBackTo10()
    {
        var config = NewConfig();
        var options = new ReadWriteSplitOptions { Enabled = true };
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "cs1", HitRate = 0 });
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "cs2", HitRate = -5 });

        ServiceCollectionExtensions.ApplyReadWriteSplit(config, options, logger: null);

        Assert.NotNull(config.SlaveConnectionConfigs);
        Assert.All(config.SlaveConnectionConfigs, s => Assert.Equal(10, s.HitRate));
    }

    [Fact]
    public void PartialInvalid_SkipsEmptyAndKeepsValid()
    {
        var config = NewConfig();
        var options = new ReadWriteSplitOptions { Enabled = true };
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "", HitRate = 10 });        // 跳过
        options.Slaves.Add(new SlaveDatabaseOption { ConnectionString = "cs2", HitRate = 8 });      // 保留

        ServiceCollectionExtensions.ApplyReadWriteSplit(config, options, logger: null);

        Assert.NotNull(config.SlaveConnectionConfigs);
        Assert.Single(config.SlaveConnectionConfigs);
        Assert.Equal("cs2", config.SlaveConnectionConfigs[0].ConnectionString);
        Assert.Equal(8, config.SlaveConnectionConfigs[0].HitRate);
    }
}
