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
                ValueI18n = x.ValueI18n,
                Order = x.Order,
                Enabled = x.Enabled,
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
        entity.ValueI18n = input.ValueI18n;
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
        entity.ValueI18n = input.ValueI18n;
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
                ValueI18n = x.ValueI18n,
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
                    ValueI18n = x.ValueI18n,
                    Order = x.Order,
                    Enabled = x.Enabled,
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

    /// <inheritdoc />
    public Task<DictionaryCategoryExportDto> ExportCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        var category = _categoryRepository.Query().FirstOrDefault(x => x.Id == categoryId);
        if (category == null)
            throw new InvalidOperationException("分类不存在");

        var items = _itemRepository.Query()
            .Where(x => x.CategoryId == categoryId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.ItemKey)
            .ToList();

        var itemIdToKey = items.ToDictionary(x => x.Id, x => x.ItemKey);

        var package = new DictionaryCategoryExportDto
        {
            FormatVersion = 1,
            ExportedAt = DateTime.Now,
            Category = new DictionaryCategoryExportCategoryDto
            {
                Code = category.Code,
                Name = category.Name,
                NameI18n = category.NameI18n,
                Category = category.Category,
                SourceType = category.SourceType,
                Enabled = category.Enabled,
                Description = category.Description,
                DescriptionI18n = category.DescriptionI18n,
                ExtraJson = category.ExtraJson,
                Module = category.Module
            },
            Items = items.Select(x => new DictionaryCategoryExportItemDto
            {
                ItemKey = x.ItemKey,
                ItemValue = x.ItemValue,
                ValueI18n = x.ValueI18n,
                Order = x.Order,
                Enabled = x.Enabled,
                ParentItemKey = x.ParentId.HasValue && itemIdToKey.TryGetValue(x.ParentId.Value, out var pk) ? pk : null,
                ExtraJson = x.ExtraJson
            }).ToList()
        };

        return Task.FromResult(package);
    }

    /// <inheritdoc />
    public async Task<DictionaryImportResultDto> ImportCategoryAsync(
        DictionaryCategoryExportDto package,
        bool overwriteIfExists = true,
        CancellationToken cancellationToken = default)
    {
        if (package?.Category == null)
            throw new InvalidOperationException("导入包格式无效：缺少 category");
        if (string.IsNullOrWhiteSpace(package.Category.Code))
            throw new InvalidOperationException("导入包格式无效：分类编码不能为空");
        if (string.IsNullOrWhiteSpace(package.Category.Name))
            throw new InvalidOperationException("导入包格式无效：分类名称不能为空");

        var code = package.Category.Code.Trim();
        var result = new DictionaryImportResultDto { CategoryCode = code };

        var existing = _categoryRepository.Query().FirstOrDefault(x => x.Code == code);
        DictionaryCategory categoryEntity;
        if (existing == null)
        {
            categoryEntity = DictionaryCategory.Create(code, package.Category.Name.Trim());
            ApplyExportedCategoryMeta(categoryEntity, package.Category);
            await _categoryRepository.AddAsync(categoryEntity, cancellationToken);
            result.CreatedCategory = true;
        }
        else
        {
            if (!overwriteIfExists)
                throw new InvalidOperationException($"分类编码 [{code}] 已存在，请勾选覆盖或修改编码后重试");
            categoryEntity = existing;
            categoryEntity.Rename(package.Category.Name.Trim());
            ApplyExportedCategoryMeta(categoryEntity, package.Category);
            await _categoryRepository.UpdateAsync(categoryEntity, cancellationToken);
            _cache.Remove($"DictItems_{code}");
        }

        result.CategoryId = categoryEntity.Id;

        var importItems = package.Items ?? new List<DictionaryCategoryExportItemDto>();
        var existingItems = _itemRepository.Query().Where(x => x.CategoryId == categoryEntity.Id).ToList();
        var existingByKey = existingItems.ToDictionary(x => x.ItemKey, StringComparer.OrdinalIgnoreCase);
        var importKeys = new HashSet<string>(importItems.Select(x => x.ItemKey.Trim()), StringComparer.OrdinalIgnoreCase);

        foreach (var row in importItems)
        {
            if (string.IsNullOrWhiteSpace(row.ItemKey)) continue;
            var key = row.ItemKey.Trim();
            if (existingByKey.TryGetValue(key, out var entity))
            {
                entity.RenameKey(key);
                entity.UpdateValue(row.ItemValue?.Trim() ?? string.Empty);
                entity.ValueI18n = row.ValueI18n;
                entity.SetOrder(row.Order);
                if (row.Enabled) entity.Enable(); else entity.Disable();
                entity.ExtraJson = row.ExtraJson;
                entity.ParentId = null;
                await _itemRepository.UpdateAsync(entity, cancellationToken);
                result.ItemsUpdated++;
            }
            else
            {
                var newEntity = DictionaryItem.Create(categoryEntity.Id, key, row.ItemValue?.Trim() ?? string.Empty, null);
                newEntity.ValueI18n = row.ValueI18n;
                newEntity.SetOrder(row.Order);
                if (row.Enabled) newEntity.Enable(); else newEntity.Disable();
                newEntity.ExtraJson = row.ExtraJson;
                await _itemRepository.AddAsync(newEntity, cancellationToken);
                existingByKey[key] = newEntity;
                result.ItemsCreated++;
            }
        }

        // 第二遍：恢复层级 parentItemKey
        foreach (var row in importItems)
        {
            if (string.IsNullOrWhiteSpace(row.ItemKey) || string.IsNullOrWhiteSpace(row.ParentItemKey)) continue;
            if (!existingByKey.TryGetValue(row.ItemKey.Trim(), out var entity)) continue;
            if (!existingByKey.TryGetValue(row.ParentItemKey.Trim(), out var parent)) continue;
            if (entity.ParentId != parent.Id)
            {
                entity.MoveTo(parent.Id);
                await _itemRepository.UpdateAsync(entity, cancellationToken);
            }
        }

        // 删除导出包中不存在的旧条目（全量同步）
        foreach (var orphan in existingItems.Where(x => !importKeys.Contains(x.ItemKey)))
        {
            await _itemRepository.DeleteAsync(orphan.Id, cancellationToken);
            result.ItemsDeleted++;
        }

        _cache.Remove($"DictItems_{code}");
        return result;
    }

    private static void ApplyExportedCategoryMeta(DictionaryCategory entity, DictionaryCategoryExportCategoryDto src)
    {
        if (src.Enabled) entity.Enable(); else entity.Disable();
        entity.ChangeMeta(src.Category, src.SourceType, src.Description, src.ExtraJson);
        entity.NameI18n = src.NameI18n;
        entity.DescriptionI18n = src.DescriptionI18n;
        if (!string.IsNullOrWhiteSpace(src.Module))
            entity.Module = src.Module.Trim();
    }
}


