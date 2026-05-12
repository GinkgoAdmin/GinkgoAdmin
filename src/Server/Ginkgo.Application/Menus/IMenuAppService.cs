// 文件功能说明：
// 定义菜单应用服务接口。

using Ginkgo.Shared;

namespace Ginkgo.Application.Menus;

/// <summary>
/// 菜单应用服务接口。
/// </summary>
public interface IMenuAppService
{
    /// <summary>
    /// 分页查询菜单。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PagedResult<MenuListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取菜单详情。
    /// </summary>
    /// <param name="id">菜单 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<MenuDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建菜单。
    /// </summary>
    /// <param name="input">创建输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<long> CreateAsync(CreateMenuInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新菜单。
    /// </summary>
    /// <param name="id">菜单 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateAsync(long id, UpdateMenuInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除菜单。
    /// </summary>
    /// <param name="id">菜单 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}





