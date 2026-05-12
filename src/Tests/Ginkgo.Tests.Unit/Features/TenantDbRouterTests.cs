// 文件功能说明：
// 验证 TenantDbRouter 的开关行为与参数校验（不需要真实数据库连接）：
//   - Disabled → IsEnabled=false，ChangeDatabase 抛 NotSupportedException，
//     GetAvailableConfigIds 返回空
//   - Enabled → IsEnabled=true，参数校验正常，未知 configId 抛 ArgumentException
//   - NullClient → ArgumentNullException
//   - NullOptions → 兜底 Disabled

using Ginkgo.Infrastructure.Persistence.Features;
using SqlSugar;

namespace Ginkgo.Tests.Unit.Features;

public sealed class TenantDbRouterTests
{
    private static ISqlSugarClient CreateClient() =>
        new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.MySql,
            ConnectionString = "Server=__marker__;",
            IsAutoCloseConnection = true,
        });

    private static SaasMultiDbOptions MakeEnabled(params (string id, string cs)[] conns)
    {
        var opts = new SaasMultiDbOptions { Enabled = true };
        foreach (var (id, cs) in conns)
        {
            opts.Connections.Add(new SaasDbConnectionOption
            {
                ConfigId = id,
                ConnectionString = cs,
                Description = $"测试租户 {id}"
            });
        }
        return opts;
    }

    // ========== Disabled ==========

    [Fact]
    public void Disabled_IsEnabled_False()
    {
        var sut = new TenantDbRouter(CreateClient(), new SaasMultiDbOptions { Enabled = false });
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public void Disabled_GetAvailableConfigIds_Empty()
    {
        var sut = new TenantDbRouter(CreateClient(), new SaasMultiDbOptions { Enabled = false });
        Assert.Empty(sut.GetAvailableConfigIds());
    }

    [Fact]
    public void Disabled_ChangeDatabase_ThrowsNotSupported()
    {
        var sut = new TenantDbRouter(CreateClient(), new SaasMultiDbOptions { Enabled = false });
        Assert.Throws<NotSupportedException>(() => sut.ChangeDatabase("tenant1"));
    }

    [Fact]
    public void Disabled_CurrentConfigId_IsNull()
    {
        var sut = new TenantDbRouter(CreateClient(), new SaasMultiDbOptions { Enabled = false });
        Assert.Null(sut.CurrentConfigId);
    }

    // ========== Enabled + 参数校验 ==========

    [Fact]
    public void Enabled_IsEnabled_True()
    {
        var sut = new TenantDbRouter(CreateClient(), MakeEnabled(("t1", "Server=t1;")));
        Assert.True(sut.IsEnabled);
    }

    [Fact]
    public void Enabled_GetAvailableConfigIds_ReturnsConfigured()
    {
        var sut = new TenantDbRouter(CreateClient(), MakeEnabled(("t1", "Server=t1;"), ("t2", "Server=t2;")));
        var ids = sut.GetAvailableConfigIds();
        Assert.Equal(2, ids.Count);
        Assert.Contains("t1", ids);
        Assert.Contains("t2", ids);
    }

    [Fact]
    public void Enabled_ChangeDatabase_NullConfigId_Throws()
    {
        var sut = new TenantDbRouter(CreateClient(), MakeEnabled(("t1", "Server=t1;")));
        Assert.Throws<ArgumentException>(() => sut.ChangeDatabase(null!));
    }

    [Fact]
    public void Enabled_ChangeDatabase_EmptyConfigId_Throws()
    {
        var sut = new TenantDbRouter(CreateClient(), MakeEnabled(("t1", "Server=t1;")));
        Assert.Throws<ArgumentException>(() => sut.ChangeDatabase(""));
    }

    [Fact]
    public void Enabled_ChangeDatabase_UnknownConfigId_ThrowsArgument()
    {
        var sut = new TenantDbRouter(CreateClient(), MakeEnabled(("t1", "Server=t1;")));
        var ex = Assert.Throws<ArgumentException>(() => sut.ChangeDatabase("unknown_tenant"));
        Assert.Contains("unknown_tenant", ex.Message);
    }

    [Fact]
    public void Enabled_NoConnections_GetAvailableConfigIds_Empty()
    {
        var opts = new SaasMultiDbOptions { Enabled = true };
        var sut = new TenantDbRouter(CreateClient(), opts);
        Assert.Empty(sut.GetAvailableConfigIds());
    }

    // ========== 构造参数 ==========

    [Fact]
    public void NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TenantDbRouter(null!, new SaasMultiDbOptions { Enabled = true }));
    }

    [Fact]
    public void NullOptions_FallbackToDefault_Disabled()
    {
        var sut = new TenantDbRouter(CreateClient(), (SaasMultiDbOptions)null!);
        Assert.False(sut.IsEnabled);
    }
}
