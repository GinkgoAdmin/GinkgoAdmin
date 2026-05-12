// 文件功能说明：
// 验证 ReportableQueryFactory 的开关行为：
//   - Enabled=false → IsEnabled=false，Create<T> 抛 NotSupportedException
//   - Enabled=true  → IsEnabled=true，Create<T> 返回非 null 的 SqlSugar IReportable<T>
//   - data=null → ArgumentNullException

using Ginkgo.Infrastructure.Persistence.Features;
using SqlSugar;

namespace Ginkgo.Tests.Unit.Features;

public sealed class ReportableQueryFactoryTests
{
    private sealed class Sample
    {
        public string Cat { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private static ISqlSugarClient CreateClient() =>
        new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.MySql,
            ConnectionString = "Server=__marker__;",
            IsAutoCloseConnection = true,
        });

    [Fact]
    public void Disabled_IsEnabled_False()
    {
        var sut = new ReportableQueryFactory(CreateClient(), new ReportableOptions { Enabled = false });
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public void Disabled_Create_ThrowsNotSupported()
    {
        var sut = new ReportableQueryFactory(CreateClient(), new ReportableOptions { Enabled = false });
        var ex = Assert.Throws<NotSupportedException>(() => sut.Create(new List<Sample>()));
        Assert.Contains("Reportable", ex.Message);
        Assert.Contains("Database.Features.Reportable.Enabled", ex.Message);
    }

    [Fact]
    public void Enabled_IsEnabled_True()
    {
        var sut = new ReportableQueryFactory(CreateClient(), new ReportableOptions { Enabled = true });
        Assert.True(sut.IsEnabled);
    }

    [Fact]
    public void Enabled_Create_ReturnsReportable()
    {
        var sut = new ReportableQueryFactory(CreateClient(), new ReportableOptions { Enabled = true });
        var data = new List<Sample>
        {
            new() { Cat = "A", Value = 1 },
            new() { Cat = "A", Value = 2 },
            new() { Cat = "B", Value = 3 },
        };

        var reportable = sut.Create(data);
        Assert.NotNull(reportable);
        Assert.IsAssignableFrom<IReportable<Sample>>(reportable);
    }

    [Fact]
    public void Create_NullData_Throws()
    {
        var sut = new ReportableQueryFactory(CreateClient(), new ReportableOptions { Enabled = true });
        Assert.Throws<ArgumentNullException>(() => sut.Create<Sample>(null!));
    }

    [Fact]
    public void NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ReportableQueryFactory(null!, new ReportableOptions { Enabled = true }));
    }

    [Fact]
    public void NullOptions_FallbackToDefault_Disabled()
    {
        // 内部构造函数接受 null options，应兜底为默认（Enabled=false）
        var sut = new ReportableQueryFactory(CreateClient(), (ReportableOptions)null!);
        Assert.False(sut.IsEnabled);
    }
}
