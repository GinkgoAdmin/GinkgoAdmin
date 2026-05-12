// 文件功能说明：
// 数据字典模块 API 控制器，占位实现。

using Ginkgo.Application.Dictionaries;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 字典接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/dictionaries")] 
[Authorize(Policy = "Permission")]
[ApiVersion("1.0")]
public sealed class DictionariesController : ControllerBase
{
    private readonly IDictionaryAppService _service;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="service">字典应用服务。</param>
    public DictionariesController(IDictionaryAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取所有字典分类（简化版本，用于下拉选择等场景）。
    /// </summary>
    [HttpGet]
    public async Task<Result<List<DictionaryCategoryListItemDto>>> GetAllCategoriesAsync()
    {
        var data = await _service.GetCategoryPagedAsync(new PageRequest { Page = 1, PageSize = 1000 }, null);
        return Result<List<DictionaryCategoryListItemDto>>.Success(data.Items.ToList());
    }

    /// <summary>
    /// 分类分页。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字。</param>
    [HttpGet("categories")]
    public async Task<Result<PagedResult<DictionaryCategoryListItemDto>>> GetCategoriesAsync([FromQuery] PageRequest request, [FromQuery] string? keyword)
    {
        var data = await _service.GetCategoryPagedAsync(request, keyword);
        return Result<PagedResult<DictionaryCategoryListItemDto>>.Success(data);
    }

    /// <summary>
    /// 根据分类编码批量获取字典条目，常用于前端获取枚举数据。
    /// </summary>
    /// <param name="codes">分类编码集合（以逗号分隔，如 "gender,status"）。</param>
    [HttpGet("by-codes")]
    public async Task<Result<Dictionary<string, List<DictionaryItemListItemDto>>>> GetItemsByCodesAsync([FromQuery] string codes)
    {
        if (string.IsNullOrWhiteSpace(codes)) 
        {
            return Result<Dictionary<string, List<DictionaryItemListItemDto>>>.Success(new Dictionary<string, List<DictionaryItemListItemDto>>());
        }

        var codeArray = codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var data = await _service.GetItemsByCodesAsync(codeArray);
        return Result<Dictionary<string, List<DictionaryItemListItemDto>>>.Success(data);
    }

    /// <summary>
    /// 获取分类详情。
    /// </summary>
    [HttpGet("categories/{id}")]
    public async Task<Result<DictionaryCategoryDetailDto>> GetCategoryAsync(long id)
    {
        var item = await _service.GetCategoryAsync(id);
        if (item == null) return Result<DictionaryCategoryDetailDto>.Fail(404, "分类不存在");
        return Result<DictionaryCategoryDetailDto>.Success(item);
    }

    /// <summary>
    /// 条目分页。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="categoryId">分类 Id（Snowflake ID）。</param>
    [HttpGet("items")]
    public async Task<Result<PagedResult<DictionaryItemListItemDto>>> GetItemsAsync([FromQuery] PageRequest request, [FromQuery] long categoryId)
    {
        var data = await _service.GetItemPagedAsync(request, categoryId);
        return Result<PagedResult<DictionaryItemListItemDto>>.Success(data);
    }

    /// <summary>
    /// 创建分类。
    /// </summary>
    /// <param name="input">创建输入。</param>
    [HttpPost("categories")]
    public async Task<Result<long>> CreateCategoryAsync([FromBody] CreateDictionaryCategoryInput input)
    {
        var id = await _service.CreateCategoryAsync(input);
        return Result<long>.Success(id, "创建成功");
    }

    /// <summary>
    /// 更新分类。
    /// </summary>
    /// <param name="id">分类 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    [HttpPut("categories/{id}")]
    public async Task<Result> UpdateCategoryAsync(long id, [FromBody] UpdateDictionaryCategoryInput input)
    {
        await _service.UpdateCategoryAsync(id, input);
        return Result.Success("更新成功");
    }

    /// <summary>
    /// 删除分类。
    /// </summary>
    /// <param name="id">分类 Id（Snowflake ID）。</param>
    [HttpDelete("categories/{id}")]
    public async Task<Result> DeleteCategoryAsync(long id)
    {
        await _service.DeleteCategoryAsync(id);
        return Result.Success("删除成功");
    }

    /// <summary>
    /// 创建条目。
    /// </summary>
    /// <param name="input">创建输入。</param>
    [HttpPost("items")]
    public async Task<Result<long>> CreateItemAsync([FromBody] CreateDictionaryItemInput input)
    {
        var id = await _service.CreateItemAsync(input);
        return Result<long>.Success(id, "创建成功");
    }

    /// <summary>
    /// 更新条目。
    /// </summary>
    /// <param name="id">条目 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    [HttpPut("items/{id}")]
    public async Task<Result> UpdateItemAsync(long id, [FromBody] UpdateDictionaryItemInput input)
    {
        await _service.UpdateItemAsync(id, input);
        return Result.Success("更新成功");
    }

    /// <summary>
    /// 删除条目。
    /// </summary>
    /// <param name="id">条目 Id（Snowflake ID）。</param>
    [HttpDelete("items/{id}")]
    public async Task<Result> DeleteItemAsync(long id)
    {
        await _service.DeleteItemAsync(id);
        return Result.Success("删除成功");
    }

    /// <summary>
    /// 获取条目详情。
    /// </summary>
    [HttpGet("items/{id}")]
    public async Task<Result<DictionaryItemDetailDto>> GetItemAsync(long id)
    {
        var data = await _service.GetItemAsync(id);
        if (data == null) return Result<DictionaryItemDetailDto>.Fail(404, "条目不存在");
        return Result<DictionaryItemDetailDto>.Success(data);
    }
}


