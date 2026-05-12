// 文件功能说明：
// 字典应用服务的基础空实现，后续将填充具体业务逻辑。

using Ginkgo.Domain;
using Ginkgo.Domain.Dictionaries;
using Ginkgo.Shared;
using Microsoft.Extensions.Caching.Memory;

namespace Ginkgo.Application.Dictionaries;

/// <summary>
/// 字典应用服务实现（占位）。
/// </summary>
public sealed class DictionaryAppService : IDictionaryAppService
{
    private readonly IRepository<DictionaryCategory> _categoryRepository;
    private readonly IRepository<DictionaryItem> _itemRepository;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="categoryRepository">分类仓储。</param>
    /// <param name="itemRepository">条目仓储。</param>
    /// <param name="cache">内存缓存。</param>
    public DictionaryAppService(IRepository<DictionaryCategory> categoryRepository, IRepository<DictionaryItem> itemRepository, IMemoryCache cache)
    {
        _categoryRepository = categoryRepository;
        _itemRepository = itemRepository;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<PagedResult<DictionaryCategoryListItemDto>> GetCategoryPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;

        var baseQuery = _categoryRepository.Query();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            baseQuery = baseQuery.Where(x => x.Name.Contains(k) || x.Code.Contains(k));
        }
        var total = baseQuery.LongCount();
        var items = baseQuery
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new DictionaryCategoryListItemDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Enabled = true, // 兼容历史库：若未建 Enabled 列则默认启用
                Category = x.Category
            })
            .ToList();
        return await Task.FromResult(new PagedResult<DictionaryCategoryListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        });
    }

    /// <inheritdoc />
    public async Task<PagedResult<DictionaryItemListItemDto>> GetItemPagedAsync(PageRequest request, long categoryId, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;

        var baseQuery = _itemRepository.Query().Where(x => x.CategoryId == categoryId);
        var total = baseQuery.LongCount();
        var items = baseQuery
            .OrderBy(x => x.Order)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new DictionaryItemListItemDto
            {
                Id = x.Id,
                CategoryId = x.CategoryId,
                ItemKey = x.ItemKey,
                ItemValue = x.ItemValue,
                ParentId = x.ParentId
            })
            .ToList();
        return await Task.FromResult(new PagedResult<DictionaryItemListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        });
    }

    /// <inheritdoc />
    public async Task<long> CreateCategoryAsync(CreateDictionaryCategoryInput input, CancellationToken cancellationToken = default)
    {
        var entity = DictionaryCategory.Create(input.Code, input.Name);
        var sourceType = input.SourceType ?? string.Empty;
        entity.ChangeMeta(input.Category, sourceType, input.Description, input.ExtraJson);
        await _categoryRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateCategoryAsync(long id, UpdateDictionaryCategoryInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return;
        entity.Rename(input.Name);
        if (input.Enabled) entity.Enable(); else entity.Disable();
        var newCategory = string.IsNullOrWhiteSpace(input.Category) ? entity.Category : input.Category;
        var newSourceType = input.SourceType ?? entity.SourceType ?? string.Empty;
        entity.ChangeMeta(newCategory, newSourceType, input.Description, input.ExtraJson);
        await _categoryRepository.UpdateAsync(entity, cancellationToken);
        _cache.Remove($"DictItems_{entity.Code}");
    }

    /// <inheritdoc />
    public async Task DeleteCategoryAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            _cache.Remove($"DictItems_{entity.Code}");
            await _categoryRepository.DeleteAsync(id, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<long> CreateItemAsync(CreateDictionaryItemInput input, CancellationToken cancellationToken = default)
    {
        // 唯一性校验：同一分类下 ItemKey 不可重复
        var duplicate = _itemRepository.Query().Any(x => x.CategoryId == input.CategoryId && x.ItemKey == input.ItemKey.Trim());
        if (duplicate)
        {
            throw new InvalidOperationException("该分类下已存在相同键，请更换后再保存。");
        }
        var entity = DictionaryItem.Create(input.CategoryId, input.ItemKey.Trim(), input.ItemValue.Trim(), input.ParentId);
        entity.ExtraJson = input.ExtraJson;
        await _itemRepository.AddAsync(entity, cancellationToken);

        var category = await _categoryRepository.GetByIdAsync(input.CategoryId, cancellationToken);
        if (category != null) _cache.Remove($"DictItems_{category.Code}");

        return entity.Id;
    }

    public Task<DictionaryCategoryDetailDto?> GetCategoryAsync(long id, CancellationToken cancellationToken = default)
    {
        var q = _categoryRepository.Query().Where(x => x.Id == id)
            .Select(x => new DictionaryCategoryDetailDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Enabled = true, // 兼容历史库：若未建 Enabled 列则默认启用
                Category = x.Category,
                SourceType = x.SourceType,
                Description = x.Description,
                ExtraJson = x.ExtraJson
            })
            .FirstOrDefault();
        return Task.FromResult(q);
    }

    /// <inheritdoc />
    public async Task UpdateItemAsync(long id, UpdateDictionaryItemInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return;
        // 唯一性校验：同一分类下 ItemKey 不可与其他记录重复
        var duplicate = _itemRepository.Query().Any(x => x.CategoryId == entity.CategoryId && x.ItemKey == input.ItemKey.Trim() && x.Id != id);
        if (duplicate)
        {
            throw new InvalidOperationException("该分类下已存在相同键，请更换后再保存。");
        }
        entity.RenameKey(input.ItemKey);
        entity.UpdateValue(input.ItemValue);
        entity.MoveTo(input.ParentId);
        entity.SetOrder(input.Order);
        if (input.Enabled) entity.Enable(); else entity.Disable();
        entity.ExtraJson = input.ExtraJson;
        await _itemRepository.UpdateAsync(entity, cancellationToken);

        var category = await _categoryRepository.GetByIdAsync(entity.CategoryId, cancellationToken);
        if (category != null) _cache.Remove($"DictItems_{category.Code}");
    }

    /// <inheritdoc />
    public async Task DeleteItemAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            var category = await _categoryRepository.GetByIdAsync(entity.CategoryId, cancellationToken);
            if (category != null) _cache.Remove($"DictItems_{category.Code}");
            await _itemRepository.DeleteAsync(id, cancellationToken);
        }
    }

    public Task<DictionaryItemDetailDto?> GetItemAsync(long id, CancellationToken cancellationToken = default)
    {
        var dto = _itemRepository.Query().Where(x => x.Id == id)
            .Select(x => new DictionaryItemDetailDto
            {
                Id = x.Id,
                CategoryId = x.CategoryId,
                ItemKey = x.ItemKey,
                ItemValue = x.ItemValue,
                Order = x.Order,
                Enabled = x.Enabled,
                ParentId = x.ParentId
            })
            .FirstOrDefault();
        return Task.FromResult(dto);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, List<DictionaryItemListItemDto>>> GetItemsByCodesAsync(string[] codes, CancellationToken cancellationToken = default)
    {
        if (codes == null || codes.Length == 0) return new Dictionary<string, List<DictionaryItemListItemDto>>();

        var result = new Dictionary<string, List<DictionaryItemListItemDto>>();
        var missingCodes = new List<string>();

        // 1. 先尝试从缓存中获取
        foreach (var code in codes)
        {
            var key = $"DictItems_{code}";
            if (_cache.TryGetValue(key, out List<DictionaryItemListItemDto>? cachedData) && cachedData != null)
            {
                result[code] = cachedData;
            }
            else
            {
                missingCodes.Add(code);
            }
        }

        // 2. 如果全都命中缓存，则直接返回
        if (missingCodes.Count == 0) return result;

        // 3. 将未命中缓存的按 Code 从数据库查出对应的 Category
        var categories = await Task.FromResult(_categoryRepository.Query()
            .Where(x => missingCodes.Contains(x.Code) && x.Enabled)
            .ToList());

        var categoryIdToCodeMap = categories.ToDictionary(x => x.Id, x => x.Code);
        var missingCategoryIds = categoryIdToCodeMap.Keys.ToList();

        if (missingCategoryIds.Count > 0)
        {
            // 4. 根据查出的 CategoryId，批量查询并按 SortOrder 排序
            var allItems = await Task.FromResult(_itemRepository.Query()
                .Where(x => missingCategoryIds.Contains(x.CategoryId) && x.Enabled)
                .OrderBy(x => x.CategoryId).OrderBy(x => x.Order)
                .Select(x => new DictionaryItemListItemDto
                {
                    Id = x.Id,
                    CategoryId = x.CategoryId,
                    ItemKey = x.ItemKey,
                    ItemValue = x.ItemValue,
                    ParentId = x.ParentId
                })
                .ToList());

            // 5. 按 CategoryId 分组，装填并回写缓存
            var groupDict = allItems.GroupBy(x => x.CategoryId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var category in categories)
            {
                var list = groupDict.TryGetValue(category.Id, out var items) ? items : new List<DictionaryItemListItemDto>();
                result[category.Code] = list;
                _cache.Set($"DictItems_{category.Code}", list, TimeSpan.FromHours(2)); // 设置2小时缓存，发生编辑时主动清除
            }
        }

        // 确保哪怕这个 code 没查到，也给个空防止重复查库
        foreach (var code in missingCodes)
        {
            if (!result.ContainsKey(code))
            {
                result[code] = new List<DictionaryItemListItemDto>();
                _cache.Set($"DictItems_{code}", result[code], TimeSpan.FromHours(1));
            }
        }

        return result;
    }
}


