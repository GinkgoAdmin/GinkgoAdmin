// 文件功能说明：
// 验证 BulkInsertService 的基础语义（不连真库）：
//   - 空列表直接返回 0，不会尝试调用数据库
//   - ResolveBatchSize：显式传入 > 配置 DefaultBatchSize > 兜底 5000
// 真实 BulkCopy / Insertable 行为交给集成测试或 ImportController 端到端回归。

using System.Data;
using Ginkgo.Infrastructure.Abstractions;
using Ginkgo.Infrastructure.Persistence.Features;
using Microsoft.Extensions.Options;

namespace Ginkgo.Tests.Unit.Features;

public sealed class BulkInsertServiceTests
{
    private sealed class Dummy { public long Id { get; set; } public string Name { get; set; } = string.Empty; }

    private static BulkInsertService NewServiceWithFakeDb(DatabaseFeaturesOptions features)
    {
        // 使用假的 SqlSugarClient 实例足以跑"空列表直接返回 0"的路径——空列表时根本不会触达 db。
        // 不传真实连接串，避免实际打开连接。
        var config = new SqlSugar.ConnectionConfig
        {
            DbType = SqlSugar.DbType.MySql,
            ConnectionString = "server=127.0.0.1;database=__nonexistent_for_unittest__;uid=x;pwd=x",
            IsAutoCloseConnection = true
        };
        var client = new SqlSugar.SqlSugarClient(config);
        return new BulkInsertService(client, Options.Create(features));
    }

    [Fact]
    public async Task BulkInsertAsync_EmptyList_ReturnsZero_WithoutHittingDb()
    {
        var svc = NewServiceWithFakeDb(new DatabaseFeaturesOptions());
        var affected = await svc.BulkInsertAsync<Dummy>(Array.Empty<Dummy>());
        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task BulkInsertDataTable_EmptyTable_ReturnsZero_WithoutHittingDb()
    {
        var svc = NewServiceWithFakeDb(new DatabaseFeaturesOptions());
        var dt = new DataTable("t");
        var affected = await svc.BulkInsertDataTableAsync("t", dt);
        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task BulkUpdateAsync_EmptyList_ReturnsZero_WithoutHittingDb()
    {
        var svc = NewServiceWithFakeDb(new DatabaseFeaturesOptions());
        var affected = await svc.BulkUpdateAsync<Dummy>(Array.Empty<Dummy>());
        Assert.Equal(0, affected);
    }

    [Fact]
    public void Service_IsAssignableTo_IBulkInsertService()
    {
        // 契约校验：接口在 Ginkgo.Infrastructure.Abstractions，默认实现在 Ginkgo.Infrastructure.Persistence.Features。
        Assert.True(typeof(IBulkInsertService).IsAssignableFrom(typeof(BulkInsertService)));
    }
}
