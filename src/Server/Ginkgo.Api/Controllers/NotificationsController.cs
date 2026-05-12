using Ginkgo.Application.Notifications;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Ginkgo.Domain.Notifications;
using SqlSugar;

namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
[ApiVersion("1.0")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotifyAppService _service;
    private readonly ISqlSugarClient _db;
    public NotificationsController(INotifyAppService service, ISqlSugarClient db)
    {
        _service = service;
        _db = db;
    }

    [HttpPost]
    public async Task<Result<long>> CreateAsync([FromBody] CreateNotifyInput input)
    {
        var id = await _service.CreateAsync(input);
        return Result<long>.Success(id, "创建成功");
    }

        [HttpGet("{id}")]
        public async Task<Result<CreateNotifyInput>> GetDetailAsync(long id)
        {
            var dto = await _service.GetDetailAsync(id);
            if (dto == null) return Result<CreateNotifyInput>.Fail(404, "不存在");
            return Result<CreateNotifyInput>.Success(dto);
        }

        [HttpPut("{id}")]
        public async Task<Result> UpdateAsync(long id, [FromBody] UpdateNotifyInput input)
        {
            await _service.UpdateAsync(id, input);
            return Result.Success("更新成功");
        }


        [HttpDelete("{id}")]
        public async Task<Result> DeleteAsync(long id)
        {
            await _service.DeleteAsync(id);
            return Result.Success("已删除");
        }

        // 软删除（针对已发布通知的下架）
        [HttpDelete("{id}/soft")]
        public async Task<Result> SoftDeleteAsync(long id)
        {
            await _service.SoftDeleteAsync(id);
            return Result.Success("已下架");
        }



    [HttpPost("{id}/publish")]
    public async Task<Result> PublishAsync(long id)
    {
        await _service.PublishAsync(id);
        return Result.Success("已发布");
    }

    [HttpGet]
    public async Task<Result<PagedResult<NotifyListItemDto>>> GetAsync([FromQuery] PageRequest request, [FromQuery] string? filter = null)
    {
        // 解析筛选参数：filter 为 JSON 字符串，形如 { "title": "...", "dateRange": ["2025-01-01", "2025-01-31"] }
        string? title = null; DateTime? from = null; DateTime? to = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(filter);
                if (dict != null)
                {
                    if (dict.TryGetValue("title", out var t) && t != null) title = t.ToString();
                    if (dict.TryGetValue("dateRange", out var dr) && dr is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array && je.GetArrayLength() == 2)
                    {
                        var s = je[0].GetString(); var e = je[1].GetString();
                        if (DateTime.TryParse(s, out var sdt)) from = sdt;
                        if (DateTime.TryParse(e, out var edt)) to = edt;
                    }
                }
            }
            catch { }
        }

        // 分页与查询（与服务层排序保持一致）
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        RefAsync<int> total = 0;

        var q = _db.Queryable<NotificationMessage>();
        if (!string.IsNullOrWhiteSpace(title)) q = q.Where(x => x.Title.Contains(title!));
        if (from != null && to != null) q = q.Where(x => x.PublishedAt >= from && x.PublishedAt <= to);

        var rows = await q
            .OrderBy(x => SqlFunc.IIF(x.PublishedAt == null, x.CreatedAt, x.PublishedAt), OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(page, size, total);

        var items = rows.Select(x => new NotifyListItemDto
        {
            Id = x.Id,
            Title = x.Title,
            PublishedAt = x.PublishedAt,
            Status = x.Status
        }).ToList();

        var result = new PagedResult<NotifyListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        };
        return Result<PagedResult<NotifyListItemDto>>.Success(result);
    }

        // 兼容老前端：返回最近200条列表（不分页）
        [HttpGet("list")]
        public async Task<Result<List<NotifyListItemDto>>> GetListCompatAsync()
        {
            var data = await _service.GetPagedAsync(new PageRequest { Page = 1, PageSize = 200 });
            return Result<List<NotifyListItemDto>>.Success(data.Items.ToList());
        }


    // 统计：发送人数、已收到人数、名单
    [HttpGet("{id}/stats")]
    public async Task<Result<NotifyStatsDto>> GetStatsAsync(long id)
    {
        var dto = await _service.GetStatsAsync(id);
        return Result<NotifyStatsDto>.Success(dto);
    }

        // 统计摘要（限制名单前10个）
        [HttpGet("{id}/stats/summary")]
        public async Task<Result<NotifyStatsSummaryDto>> GetStatsSummaryAsync(long id)
        {
            var dto = await _service.GetStatsSummaryAsync(id);
            return Result<NotifyStatsSummaryDto>.Success(dto);
        }

        // 已发布通知详情（含附件与名单分页/搜索）
        [HttpGet("{id}/detail")]
        public async Task<Result<PublishedNotifyDetailDto>> GetPublishedDetailAsync(long id, [FromQuery] string? listType, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var dto = await _service.GetPublishedDetailAsync(id, listType, q, page, pageSize);
            if (dto == null) return Result<PublishedNotifyDetailDto>.Fail(404, "不存在或未发布");
            return Result<PublishedNotifyDetailDto>.Success(dto);
        }


    // 我的通知
    [HttpGet("my")]
    [AllowAnonymous]
    public async Task<Result<List<MyNotifyListItemDto>>> GetMyAsync([FromServices] IHttpContextAccessor accessor, [FromQuery] string? filter = null)
    {
        // 允许匿名，但必须有 JWT 标识用户
        var ctxUser = accessor.HttpContext?.User ?? User;
        var uid = ctxUser?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctxUser?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Result<List<MyNotifyListItemDto>>.Fail(401, "未登录");
        // 解析筛选：{ title, dateRange: [from, to] }
        string? title = null; DateTime? from = null; DateTime? to = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(filter);
                if (dict != null)
                {
                    if (dict.TryGetValue("title", out var t) && t != null) title = t.ToString();
                    if (dict.TryGetValue("dateRange", out var dr) && dr is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array && je.GetArrayLength() == 2)
                    {
                        var s = je[0].GetString(); var e = je[1].GetString();
                        if (DateTime.TryParse(s, out var sdt)) from = sdt;
                        if (DateTime.TryParse(e, out var edt)) to = edt;
                    }
                }
            }
            catch { }
        }

        var list = await _service.GetMyListAsync(userId);
        if (!string.IsNullOrWhiteSpace(title)) list = list.Where(x => x.Title != null && x.Title.Contains(title)).ToList();
        if (from != null && to != null) list = list.Where(x => (x.PublishedAt ?? DateTime.MinValue) >= from && (x.PublishedAt ?? DateTime.MinValue) <= to).ToList();
        return Result<List<MyNotifyListItemDto>>.Success(list);
    }

    [HttpGet("my/{id}")]
    [AllowAnonymous]
    public async Task<Result<MyNotifyDetailDto>> GetMyDetailAsync(long id, [FromServices] IHttpContextAccessor accessor)
    {
        var ctxUser = accessor.HttpContext?.User ?? User;
        var uid = ctxUser?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctxUser?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Result<MyNotifyDetailDto>.Fail(401, "未登录");
        var dto = await _service.GetMyDetailAsync(userId, id);
        if (dto == null) return Result<MyNotifyDetailDto>.Fail(404, "不存在");
        return Result<MyNotifyDetailDto>.Success(dto);
    }

    [HttpPost("my/{id}/read")]
    [AllowAnonymous]
    public async Task<Result> MarkReadAsync(long id, [FromServices] IHttpContextAccessor accessor)
    {
        var ctxUser = accessor.HttpContext?.User ?? User;
        var uid = ctxUser?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctxUser?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Result.Fail(401, "未登录");
        await _service.MarkReadAsync(userId, id);
        return Result.Success("已读");
    }

    [HttpGet("my/unread-count")]
    [AllowAnonymous]
    public async Task<Result<int>> GetUnreadCountAsync([FromServices] IHttpContextAccessor accessor)
    {
        var ctxUser = accessor.HttpContext?.User ?? User;
        var uid = ctxUser?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctxUser?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Result<int>.Fail(401, "未登录");
        var n = await _service.GetUnreadCountAsync(userId);
        return Result<int>.Success(n);
    }

    // 获取通知附件列表
    [HttpGet("{id}/attachments")]
    [AllowAnonymous]
    public async Task<Result<List<AttachmentDto>>> GetAttachmentsAsync(long id, [FromServices] IHttpContextAccessor accessor)
    {
        // 只需登录即可访问附件列表
        var ctxUser = accessor.HttpContext?.User ?? User;
        var uid = ctxUser?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctxUser?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var _)) return Result<List<AttachmentDto>>.Fail(401, "未登录");

        var attachments = await _db.Queryable<AttachmentVO>()
            .Where(a => a.NotifyId == id)
            .Select(a => new AttachmentDto
            {
                FileId = a.FileId,
                Name = a.Name,
                ContentType = a.ContentType,
                Size = a.Size
            })
            .ToListAsync();

        // 从文件表补齐 Size 和 FileUrl
        var allFileIds = attachments.Select(x => x.FileId).Distinct().ToList();
        if (allFileIds.Count > 0)
        {
            var fileInfos = await _db.Queryable<Ginkgo.Domain.Files.SysFile>()
                .Where(f => allFileIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Size, f.Url })
                .ToListAsync();
            var dict = fileInfos.ToDictionary(x => x.Id);
            foreach (var a in attachments)
            {
                if (dict.TryGetValue(a.FileId, out var info))
                {
                    if (a.Size == null) a.Size = info.Size;
                    a.FileUrl = info.Url;
                }
            }
        }
        return Result<List<AttachmentDto>>.Success(attachments);
    }

    // 下载通知附件
    [HttpGet("{id}/attachments/{fileId}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadAttachmentAsync(long id, long fileId, [FromServices] IHttpContextAccessor accessor)
    {
        // 安全修复：必须登录，且必须是该通知的收件人（或超管）才能下载附件
        var ctxUser = accessor.HttpContext?.User ?? User;
        var uid = ctxUser?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctxUser?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Unauthorized("未登录");
        if (!await IsRecipientOrSuperAdminAsync(userId, id))
            return Forbid();

        // 验证附件是否属于该通知
        var attachment = await _db.Queryable<AttachmentVO>()
            .Where(a => a.NotifyId == id && a.FileId == fileId)
            .FirstAsync();

        if (attachment == null)
            return NotFound("附件不存在");

        // 重定向到文件下载接口
        return Redirect($"/api/v1/files/{fileId}/download");
    }

    /// <summary>
    /// 判断当前用户是否有权访问指定通知的附件：
    /// 1) 属于通知收件人；2) 是发布者；3) 是超级管理员角色或 ADMIN 旧角色。
    /// </summary>
    private async Task<bool> IsRecipientOrSuperAdminAsync(long userId, long notifyId)
    {
        // 超管兜底：兼容旧的字符串角色 "ADMIN"
        if (User?.IsInRole("ADMIN") == true) return true;

        // 通过 Role.IsSuperAdmin 标记判断
        try
        {
            var userRoleRepo = HttpContext.RequestServices.GetRequiredService<Ginkgo.Domain.IRepository<Ginkgo.Domain.Users.UserRole>>();
            var roleRepo = HttpContext.RequestServices.GetRequiredService<Ginkgo.Domain.IRepository<Ginkgo.Domain.Roles.Role>>();
            var roleIds = userRoleRepo.Query().Where(x => x.UserId == userId).Select(x => x.RoleId).Distinct().ToList();
            if (roleIds.Count > 0 && roleRepo.Query().Any(r => roleIds.Contains(r.Id) && r.Enabled && r.IsSuperAdmin))
                return true;
        }
        catch { /* ignore role lookup failure, fall back to recipient check */ }

        // 检查是否为通知的收件人：依赖 GetMyDetailAsync（非收件人返回 null）
        try
        {
            var detail = await _service.GetMyDetailAsync(userId, notifyId);
            return detail != null;
        }
        catch
        {
            return false;
        }
    }
}


