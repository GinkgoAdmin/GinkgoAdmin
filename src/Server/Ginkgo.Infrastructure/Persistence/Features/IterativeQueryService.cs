// 文件功能说明：
// IIterativeQueryService 默认实现。基于 SqlSugar 的 ToPageListAsync 循环分页拉取，
// 直到某一页返回 0 行或回调请求停止。
//
// 注意：
// - 调用方传入的 ISugarQueryable<T> 已包含筛选 / 排序；本服务不再追加排序。
//   未设置排序时，分页结果在多数数据库上不保证稳定，可能漏处理或重复处理；调用方需自行 OrderBy。
// - 本服务自身无开关；行为是"通用分批工具"，与 Database.Features 无关。

using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 通用分批迭代查询服务的默认实现（基于 SqlSugar ToPageListAsync 循环）。
/// </summary>
public sealed class IterativeQueryService : IIterativeQueryService
{
    /// <inheritdoc />
    public async Task<long> PageEachAsync<T>(
        ISugarQueryable<T> queryable,
        int pageSize,
        Func<IReadOnlyList<T>, int, Task> pageHandler,
        CancellationToken ct = default)
        where T : class, new()
    {
        if (pageHandler == null) throw new ArgumentNullException(nameof(pageHandler));
        return await PageEachUntilAsync(queryable, pageSize, async (items, idx) =>
        {
            await pageHandler(items, idx).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> PageEachUntilAsync<T>(
        ISugarQueryable<T> queryable,
        int pageSize,
        Func<IReadOnlyList<T>, int, Task<bool>> pageHandler,
        CancellationToken ct = default)
        where T : class, new()
    {
        if (queryable == null) throw new ArgumentNullException(nameof(queryable));
        if (pageHandler == null) throw new ArgumentNullException(nameof(pageHandler));

        var size = pageSize > 0 ? pageSize : 1000;
        long total = 0;
        var pageIndex = 1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await queryable.ToPageListAsync(pageIndex, size).ConfigureAwait(false);
            if (page == null || page.Count == 0)
            {
                break;
            }

            total += page.Count;
            var continueIter = await pageHandler(page, pageIndex).ConfigureAwait(false);
            if (!continueIter)
            {
                break;
            }

            // 当前页不足一整批时已经是最后一页。
            if (page.Count < size)
            {
                break;
            }

            pageIndex++;
        }

        return total;
    }
}
