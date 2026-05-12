using Ginkgo.Api.Services;
using Ginkgo.Plugin.Abstractions;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 接口注释查询 API（横切能力）。
/// <para>
/// 专供运维/监控/审计类横切页面（如资源监控的实时采样列表、异常事件流、接口审计面板等）
/// 按 <c>HTTP Method + Path</c> 反查被标注的业务说明。
/// 业务插件不应通过此 API 获取业务语义，仅用于给运维人员展示"这个接口是做什么的"。
/// </para>
/// </summary>
[ApiController]
[Route("api/endpoint-descriptions")]
[Authorize(Policy = "Permission")]
public sealed class EndpointDescriptionsController : ControllerBase
{
    private readonly EndpointDescriptionCatalog _catalog;

    public EndpointDescriptionsController(EndpointDescriptionCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// 单条查询接口注释。
    /// </summary>
    /// <param name="method">HTTP 方法，大小写不敏感，留空视为任意方法。</param>
    /// <param name="path">请求路径（含或不含前导 <c>/</c> 均可）。</param>
    [HttpGet("resolve")]
    [EndpointComment("查询单个接口标题", Category = "只读")]
    public IActionResult Resolve([FromQuery] string? method, [FromQuery] string path)
    {
        var entry = _catalog.Resolve(method, path);
        return Ok(Result<object>.Success(MapEntry(entry)));
    }

    /// <summary>
    /// 批量查询接口注释。
    /// </summary>
    [HttpPost("batch")]
    [EndpointComment("批量查询接口标题", Category = "只读")]
    public IActionResult Batch([FromBody] EndpointDescriptionBatchInput input)
    {
        if (input?.Items == null || input.Items.Count == 0)
        {
            return Ok(Result<List<EndpointDescriptionItemDto>>.Success(new List<EndpointDescriptionItemDto>()));
        }

        var list = new List<EndpointDescriptionItemDto>(input.Items.Count);
        foreach (var q in input.Items)
        {
            var entry = _catalog.Resolve(q.Method, q.Path);
            list.Add(new EndpointDescriptionItemDto
            {
                Method = q.Method,
                Path = q.Path,
                Description = entry?.Description,
                Category = entry?.Category,
                Template = entry?.Template,
                FromController = entry?.FromController ?? false
            });
        }

        return Ok(Result<List<EndpointDescriptionItemDto>>.Success(list));
    }

    private static object? MapEntry(EndpointCommentEntry? entry)
    {
        if (entry == null) return null;
        return new
        {
            description = entry.Description,
            category = entry.Category,
            template = entry.Template,
            fromController = entry.FromController
        };
    }
}

/// <summary>
/// 批量查询接口注释的入参。
/// </summary>
public sealed class EndpointDescriptionBatchInput
{
    public List<EndpointDescriptionQuery> Items { get; set; } = new();
}

public sealed class EndpointDescriptionQuery
{
    public string? Method { get; set; }
    public string? Path { get; set; }
}

public sealed class EndpointDescriptionItemDto
{
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Template { get; set; }
    public bool FromController { get; set; }
}
