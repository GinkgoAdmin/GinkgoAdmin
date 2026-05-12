// 文件功能说明：
// 消息通知接口：提供消息列表查询、详情获取、已读标记、全部已读、未读计数等功能。

using Ginkgo.Application.Messages;
using Ginkgo.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 消息通知接口。
/// </summary>
[ApiController]
[Route("api/message")]
[Authorize]
public sealed class MessageController : ControllerBase
{
    private readonly IMessageAppService _messageApp;

    public MessageController(IMessageAppService messageApp)
    {
        _messageApp = messageApp;
    }

    /// <summary>
    /// 获取当前用户 Id（从 JWT Claims 中提取）。
    /// </summary>
    private long GetCurrentUserId()
    {
        var uid = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var userId))
            return userId;
        throw new UnauthorizedAccessException("无法获取用户身份");
    }

    /// <summary>
    /// 创建消息（支持主送 + 知会两组接收对象、附件和链接）。
    /// </summary>
    /// <param name="input">消息创建输入。</param>
    [HttpPost]
    public async Task<Result> CreateAsync([FromBody] CreateMessageInput input)
    {
        try
        {
            await _messageApp.CreateAsync(input);
            return Result.Success("创建成功");
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = 400;
            return Result.Fail(400, ex.Message);
        }
    }

    /// <summary>
    /// 分页查询消息列表。
    /// </summary>
    /// <param name="pageIndex">页号（从 1 开始）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="isRead">已读状态筛选（null 表示不筛选）。</param>
    /// <param name="deliveryRole">送达角色筛选（null 表示不筛选）。</param>
    [HttpGet("list")]
    public async Task<Result<PagedResult<MessageListItemDto>>> GetListAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        [FromQuery] string? deliveryRole = null)
    {
        var userId = GetCurrentUserId();
        var result = await _messageApp.GetPagedListAsync(userId, pageIndex, pageSize, isRead, deliveryRole);
        return Result<PagedResult<MessageListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取消息详情。
    /// </summary>
    /// <param name="id">消息 Id。</param>
    /// <param name="platform">平台类型，用于过滤链接（null 表示返回所有链接）。</param>
    [HttpGet("{id:long}")]
    public async Task<Result<MessageDetailDto>> GetDetailAsync(long id, [FromQuery] string? platform = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var detail = await _messageApp.GetDetailAsync(id, userId, platform);
            return Result<MessageDetailDto>.Success(detail);
        }
        catch (KeyNotFoundException ex)
        {
            Response.StatusCode = 404;
            return Result<MessageDetailDto>.Fail(404, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.StatusCode = 403;
            return Result<MessageDetailDto>.Fail(403, ex.Message);
        }
    }

    /// <summary>
    /// 标记单条消息为已读。
    /// </summary>
    /// <param name="id">消息 Id。</param>
    [HttpPut("{id:long}/read")]
    public async Task<Result> MarkAsReadAsync(long id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _messageApp.MarkAsReadAsync(id, userId);
            return Result.Success("已标记为已读");
        }
        catch (KeyNotFoundException ex)
        {
            Response.StatusCode = 404;
            return Result.Fail(404, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.StatusCode = 403;
            return Result.Fail(403, ex.Message);
        }
    }

    /// <summary>
    /// 将当前用户所有未读消息标记为已读。
    /// </summary>
    [HttpPut("read-all")]
    public async Task<Result> MarkAllAsReadAsync()
    {
        var userId = GetCurrentUserId();
        await _messageApp.MarkAllAsReadAsync(userId);
        return Result.Success("已全部标记为已读");
    }

    /// <summary>
    /// 获取当前用户的未读消息数量。
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<Result<int>> GetUnreadCountAsync()
    {
        var userId = GetCurrentUserId();
        var count = await _messageApp.GetUnreadCountAsync(userId);
        return Result<int>.Success(count);
    }

    /// <summary>
    /// 管理端：分页查询已发送的消息列表。
    /// </summary>
    [HttpGet("admin/list")]
    public async Task<Result<PagedResult<AdminMessageListItemDto>>> GetAdminListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? title = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
        DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);
        var result = await _messageApp.GetAdminPagedListAsync(page, pageSize, title, start, end);
        return Result<PagedResult<AdminMessageListItemDto>>.Success(result);
    }

    /// <summary>
    /// 管理端：获取消息投递统计。
    /// </summary>
    [HttpGet("admin/stats")]
    public async Task<Result<AdminMessageStatsDto>> GetAdminStatsAsync(
        [FromQuery] string title,
        [FromQuery] string createdAt)
    {
        var dt = DateTime.Parse(createdAt);
        var stats = await _messageApp.GetAdminStatsAsync(title, dt);
        return Result<AdminMessageStatsDto>.Success(stats);
    }

    /// <summary>
    /// 管理端：获取消息批次详情（含正文、附件和链接）。
    /// </summary>
    [HttpGet("admin/detail")]
    public async Task<Result<AdminMessageDetailDto>> GetAdminDetailAsync(
        [FromQuery] string title,
        [FromQuery] string createdAt)
    {
        try
        {
            var dt = DateTime.Parse(createdAt);
            var detail = await _messageApp.GetAdminDetailAsync(title, dt);
            return Result<AdminMessageDetailDto>.Success(detail);
        }
        catch (KeyNotFoundException ex)
        {
            Response.StatusCode = 404;
            return Result<AdminMessageDetailDto>.Fail(404, ex.Message);
        }
    }

    /// <summary>
    /// 管理端：删除一批消息。
    /// </summary>
    [HttpDelete("admin/batch")]
    public async Task<Result> DeleteBatchAsync(
        [FromQuery] string title,
        [FromQuery] string createdAt)
    {
        var dt = DateTime.Parse(createdAt);
        await _messageApp.DeleteBatchAsync(title, dt);
        return Result.Success("删除成功");
    }
}
