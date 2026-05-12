// 文件功能说明：
// 定义字典应用服务接口。

using Ginkgo.Shared;

namespace Ginkgo.Application.Dictionaries;

/// <summary>
/// 字典应用服务接口。
/// </summary>
public interface IDictionaryAppService
{
    /// <summary>
    /// 分页查询字典分类。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PagedResult<DictionaryCategoryListItemDto>> GetCategoryPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询字典条目。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="categoryId">分类 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PagedResult<DictionaryItemListItemDto>> GetItemPagedAsync(PageRequest request, long categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建分类。
    /// </summary>
    /// <param name="input">创建输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<long> CreateCategoryAsync(CreateDictionaryCategoryInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新分类。
    /// </summary>
    /// <param name="id">分类 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateCategoryAsync(long id, UpdateDictionaryCategoryInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除分类。
    /// </summary>
    /// <param name="id">分类 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteCategoryAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分类详情。
    /// </summary>
    Task<DictionaryCategoryDetailDto?> GetCategoryAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取条目详情。
    /// </summary>
    Task<DictionaryItemDetailDto?> GetItemAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建条目。
    /// </summary>
    /// <param name="input">创建输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<long> CreateItemAsync(CreateDictionaryItemInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新条目。
    /// </summary>
    /// <param name="id">条目 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateItemAsync(long id, UpdateDictionaryItemInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除条目。
    /// </summary>
    /// <param name="id">条目 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteItemAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>
    /// 根据分类编码批量获取字典条目，常用于前端获取枚举数据。
    /// </summary>
    /// <param name="codes">分类编码集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Dictionary<string, List<DictionaryItemListItemDto>>> GetItemsByCodesAsync(string[] codes, CancellationToken cancellationToken = default);
}





