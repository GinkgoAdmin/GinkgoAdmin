// 文件功能说明：
// 定义通用的分页结果模型与输入参数模型，用于分页查询返回规范化结构。

namespace Ginkgo.Shared;

/// <summary>
/// 分页结果。
/// </summary>
/// <typeparam name="T">数据项类型。</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// 总记录数。
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 当前页号（从 1 开始）。
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页大小。
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 数据列表。
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}

/// <summary>
/// 分页请求参数。
/// </summary>
public sealed class PageRequest
{
    /// <summary>
    /// 页号（从 1 开始）。
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页大小。
    /// </summary>
    public int PageSize { get; set; } = 20;
}






