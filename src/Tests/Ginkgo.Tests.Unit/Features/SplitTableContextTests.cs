// 文件功能说明：
// 验证 SplitTableContext 的开关行为（不需要真实数据库连接）：
//   - Enabled=false → IsEnabled=false，所有 CRUD 抛 NotSupportedException
//   - Enabled=true  → IsEnabled=true，参数校验正常
//   - null 实体 / null predicate → ArgumentNullException

using Ginkgo.Infrastructure.Persistence.Features;
using SqlSugar;
using System.Linq.Expressions;

// 消除与 SqlSugar.SplitTableContext 的命名冲突
using SplitTableCtx = Ginkgo.Infrastructure.Persistence.Features.SplitTableContext;

namespace Ginkgo.Tests.Unit.Features;

public sealed class SplitTableContextTests
{
    private static ISqlSugarClient CreateClient() =>
        new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.MySql,
            ConnectionString = "Server=__marker__;",
            IsAutoCloseConnection = true,
        });

    private sealed class FakeOrder
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // ========== Disabled ==========

    [Fact]
    public void Disabled_IsEnabled_False()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = false });
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public async Task Disabled_InsertSingle_ThrowsNotSupported()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = false });
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.InsertAsync(new FakeOrder()));
    }

    [Fact]
    public async Task Disabled_InsertList_ThrowsNotSupported()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = false });
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.InsertAsync(new List<FakeOrder> { new() }));
    }

    [Fact]
    public void Disabled_QueryByRange_ThrowsNotSupported()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = false });
        Assert.Throws<NotSupportedException>(() =>
            sut.QueryByRange<FakeOrder>(DateTime.Now.AddMonths(-1), DateTime.Now));
    }

    [Fact]
    public async Task Disabled_Update_ThrowsNotSupported()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = false });
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.UpdateAsync(new FakeOrder()));
    }

    [Fact]
    public async Task Disabled_Delete_ThrowsNotSupported()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = false });
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.DeleteAsync<FakeOrder>(x => x.Id > 0));
    }

    // ========== Enabled + 参数校验 ==========

    [Fact]
    public void Enabled_IsEnabled_True()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = true });
        Assert.True(sut.IsEnabled);
    }

    [Fact]
    public async Task Enabled_InsertSingle_NullEntity_Throws()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = true });
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.InsertAsync((FakeOrder)null!));
    }

    [Fact]
    public async Task Enabled_InsertList_NullList_Throws()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = true });
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.InsertAsync((List<FakeOrder>)null!));
    }

    [Fact]
    public async Task Enabled_Update_NullEntity_Throws()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = true });
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.UpdateAsync<FakeOrder>(null!));
    }

    [Fact]
    public async Task Enabled_Delete_NullPredicate_Throws()
    {
        var sut = new SplitTableCtx(CreateClient(), new SplitTableOptions { Enabled = true });
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.DeleteAsync<FakeOrder>(null!));
    }

    [Fact]
    public void NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SplitTableCtx(null!, new SplitTableOptions { Enabled = true }));
    }

    [Fact]
    public void NullOptions_FallbackToDefault_Disabled()
    {
        var sut = new SplitTableCtx(CreateClient(), (SplitTableOptions)null!);
        Assert.False(sut.IsEnabled);
    }
}
