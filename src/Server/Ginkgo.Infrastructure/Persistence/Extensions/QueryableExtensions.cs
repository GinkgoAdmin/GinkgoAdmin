// 文件功能说明：
// 提供 IQueryable 的分页扩展方法，统一分页处理与结果包装。

using Ginkgo.Shared;

namespace Ginkgo.Infrastructure.Persistence.Extensions;

/// <summary>
/// 查询扩展方法。
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 分页查询并返回 <see cref="PagedResult{T}"/>。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="query">查询表达式。</param>
    /// <param name="request">分页参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;

        var total = (long)query.LongCount();
        var items = query.Skip((page - 1) * size).Take(size).ToList();

        return Task.FromResult(new PagedResult<T>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        });
    }
}

