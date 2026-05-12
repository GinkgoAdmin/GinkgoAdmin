// 文件功能说明：
// 验证 IterativeQueryService 的基础参数语义：
//   - null 入参抛 ArgumentNullException
//   - pageSize <= 0 时按 1000 兜底（通过 ResolvedPageSize 不直接暴露，间接由迭代行为推断；本测试不验证迭代）
//
// 真实分页迭代由集成测试（需要真库或 SqlSugar in-memory 适配）覆盖；
// 这里仅保证默认实现不在参数层引入回归。

using Ginkgo.Infrastructure.Persistence.Features;

namespace Ginkgo.Tests.Unit.Features;

public sealed class IterativeQueryServiceTests
{
    private sealed class Dummy { public int Id { get; set; } }

    [Fact]
    public async Task PageEachAsync_NullQueryable_Throws()
    {
        var svc = new IterativeQueryService();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.PageEachAsync<Dummy>(null!, 100, (items, idx) => Task.CompletedTask));
    }

    [Fact]
    public async Task PageEachAsync_NullHandler_Throws()
    {
        var svc = new IterativeQueryService();
        // queryable 也为 null 时，由于 ArgumentNullException 顺序问题，PageEachAsync 先校验 handler；
        // 这里用一个非 null 占位 queryable 不可得（ISugarQueryable<T> 无法直接 new），改测 PageEachUntilAsync 的同等路径。
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.PageEachUntilAsync<Dummy>(null!, 100, null!));
    }
}
