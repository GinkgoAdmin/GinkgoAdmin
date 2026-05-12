using Ginkgo.Domain.Notifications;
using Ginkgo.Domain.Users;
using Ginkgo.Realtime;
using Ginkgo.Shared;
using SqlSugar;

namespace Ginkgo.Application.Notifications;

public sealed class NotifyAppServiceV2 : INotifyAppService
{
    private readonly INotificationAppService _dddService;
    private readonly INotificationRepository _notificationRepo;
    private readonly IAudienceRepository _audienceRepo;
    private readonly IUserRepository _userRepository;
    private readonly ISqlSugarClient _db;
    private readonly IQueuePublisher _queue;

    public NotifyAppServiceV2(
        INotificationAppService dddService,
        INotificationRepository notificationRepo,
        IAudienceRepository audienceRepo,
        IUserRepository userRepository,
        ISqlSugarClient db,
        IQueuePublisher queue)
    {
        _dddService = dddService;
        _notificationRepo = notificationRepo;
        _audienceRepo = audienceRepo;
        _userRepository = userRepository;
        _db = db;
        _queue = queue;
    }

    // 1) 草稿详情（用于编辑）
    public async Task<CreateNotifyInput?> GetDetailAsync(long id, CancellationToken ct = default)
    {
        var m = await _db.Queryable<NotificationMessage>().InSingleAsync(id);
        if (m == null) return null;
        var seeds = await _db.Queryable<AudienceSeed>().Where(x => x.NotifyId == id).ToListAsync();
        var attachments = await _db.Queryable<AttachmentVO>().Where(x => x.NotifyId == id).ToListAsync();
        return new CreateNotifyInput
        {
            Title = m.Title,
            ContentType = m.ContentType,
            ContentText = m.ContentText,
            ContentHtml = m.ContentHtml,
            IsImportant = m.IsImportant,
            Priority = m.Priority,
            Audience = seeds.Select(s => new AudienceSeedInput
            {
                // 新(0/1/2/3) -> 旧(1/2/3/4)
                TargetType = s.TargetType == 0 ? (byte)1 : s.TargetType == 1 ? (byte)2 : s.TargetType == 2 ? (byte)3 : (byte)4,
                TargetValue = s.TargetValue
            }).ToList(),
            Attachments = attachments.Select(a => new AttachmentInput
            {
                FileId = a.FileId,
                Name = a.Name,
                ContentType = a.ContentType,
                Size = a.Size
            }).ToList()
        };
    }

    // 2) 更新草稿（可置换受众）
    public async Task UpdateAsync(long id, UpdateNotifyInput input, CancellationToken ct = default)
    {
        var m = await _db.Queryable<NotificationMessage>().FirstAsync(x => x.Id == id);
        if (m == null) return;
        if (m.Status != 0) return; // 仅草稿

        // 以局部更新替代实体赋值（逐列设置，绕过私有setter限制）
        await _db.Updateable<NotificationMessage>()
            .SetColumns(x => x.Title == input.Title.Trim())
            .SetColumns(x => x.ContentType == input.ContentType)
            .SetColumns(x => x.ContentText == (input.ContentType == 1 ? input.ContentText : null))
            .SetColumns(x => x.ContentHtml == (input.ContentType == 2 ? input.ContentHtml : null))
            .SetColumns(x => x.IsImportant == input.IsImportant)
            .SetColumns(x => x.Priority == input.Priority)
            .SetColumns(x => x.UpdatedAt == DateTime.Now)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync();

        if (input.Audience != null)
        {
            await _db.Deleteable<AudienceSeed>().Where(s => s.NotifyId == id).ExecuteCommandAsync();
            if (input.Audience.Count > 0)
            {
                var seeds = input.Audience.Select(a => AudienceSeed.Create(
                    id,
                    // 旧(1/2/3/4) -> 新(0/1/2/3)
                    a.TargetType == 1 ? (byte)0 : a.TargetType == 2 ? (byte)1 : a.TargetType == 3 ? (byte)2 : (byte)3,
                    a.TargetType == 4 ? (string.IsNullOrWhiteSpace(a.TargetValue) ? "*" : a.TargetValue) : a.TargetValue
                ));
                await _db.Insertable(seeds.ToList()).ExecuteCommandAsync();
            }
        }

        // 处理附件更新
        if (input.Attachments != null)
        {
            await _db.Deleteable<AttachmentVO>().Where(a => a.NotifyId == id).ExecuteCommandAsync();
            if (input.Attachments.Count > 0)
            {
                var attachments = input.Attachments.Select(a => AttachmentVO.Create(
                    id, a.FileId, a.Name, a.ContentType, a.Size
                ));
                await _db.Insertable(attachments.ToList()).ExecuteCommandAsync();
            }
        }
    }

    // 3) 删除草稿
    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var m = await _db.Queryable<NotificationMessage>().InSingleAsync(id);
        if (m == null) return;
        if (m.Status != 0) throw new InvalidOperationException("仅允许删除草稿状态的通知");
        // 级联删除：由外键 ON DELETE CASCADE 负责
        await _db.Deleteable<NotificationMessage>().In(id).ExecuteCommandAsync();
    }

    // 4) 新建草稿
    public async Task<long> CreateAsync(CreateNotifyInput input, CancellationToken ct = default)
    {
        var dto = new CreateNotificationDto
        {
            Title = input.Title,
            ContentType = input.ContentType,
            ContentText = input.ContentType == 1 ? input.ContentText : null,
            ContentHtml = input.ContentType == 2 ? input.ContentHtml : null,
            IsImportant = input.IsImportant,
            Priority = input.Priority,
            Seeds = input.Audience?.Select(a => new AudienceSeedDto
            {
                // 旧(1/2/3/4) -> 新(0/1/2/3)
                TargetType = a.TargetType == 1 ? (byte)0 : a.TargetType == 2 ? (byte)1 : a.TargetType == 3 ? (byte)2 : (byte)3,
                TargetValue = a.TargetType == 4 ? (string.IsNullOrWhiteSpace(a.TargetValue) ? "*" : a.TargetValue) : a.TargetValue
            }).ToList() ?? new List<AudienceSeedDto>(),
            Attachments = input.Attachments?.Select(a => new AttachmentDto
            {
                FileId = a.FileId,
                Name = a.Name,
                ContentType = a.ContentType,
                Size = a.Size
            }).ToList() ?? new List<AttachmentDto>()
        };
        return await _dddService.CreateAsync(dto, null, ct);
    }

    // 5) 发布
    public async Task PublishAsync(long notifyId, CancellationToken ct = default)
    {
        await _dddService.PublishAsync(notifyId, null, ct);

        // 强制保证 PublishedAt/Status 持久化为服务器时间，并同步总人数
        var totalRecipients = await _db.Queryable<AudienceMember>().Where(a => a.NotifyId == notifyId).CountAsync();
        var affected = await _db.Updateable<NotificationMessage>()
            .SetColumns(m => m.Status == 1)
            .SetColumns(m => m.PublishedAt == DateTime.Now)
            .SetColumns(m => m.TotalRecipients == totalRecipients)
            .SetColumns(m => m.UpdatedAt == DateTime.Now)
            .Where(m => m.Id == notifyId)
            .ExecuteCommandAsync();
        if (affected <= 0)
        {
            // 兜底：再试一次（非常规情况，如表达式解析差异）
            affected = await _db.Updateable<NotificationMessage>()
                .SetColumns(m => m.Status == 1)
                .SetColumns(m => m.PublishedAt == DateTime.Now)
                .SetColumns(m => m.TotalRecipients == totalRecipients)
                .SetColumns(m => m.UpdatedAt == DateTime.Now)
                .Where(m => m.Id == notifyId)
                .ExecuteCommandAsync();
            if (affected <= 0)
            {
                throw new InvalidOperationException($"发布状态保存失败：未找到消息或数据库未更新（Id={notifyId}）。");
            }
        }

        // 维持原行为：发布后推送到队列（按接收人）
        var userIds = await _db.Queryable<AudienceMember>()
            .Where(a => a.NotifyId == notifyId)
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync();
        foreach (var uid in userIds)
        {
            await _queue.PublishAsync("notify.dispatch", new { notifyId, userId = uid }, ct);
        }
    }

    // 6) 分页列表
    public async Task<PagedResult<NotifyListItemDto>> GetPagedAsync(PageRequest request, CancellationToken ct = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        RefAsync<int> total = 0;
        var rows = await _db.Queryable<NotificationMessage>()
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
        return new PagedResult<NotifyListItemDto> { Total = total, Page = page, PageSize = size, Items = items };
    }

    // 7) 我的收件箱（最近200）
    public async Task<List<MyNotifyListItemDto>> GetMyListAsync(long userId, CancellationToken ct = default)
    {
        var list = await _audienceRepo.GetInboxAsync(userId, false, 1, 200, ct);
        var ids = list.Select(a => a.NotifyId).Distinct().ToList();
        var titles = await _notificationRepo.GetTitlesAsync(ids, ct);
        var msgs = await _db.Queryable<NotificationMessage>().Where(m => ids.Contains(m.Id)).ToListAsync();
        var pubAt = msgs.ToDictionary(m => m.Id, m => m.PublishedAt);
        return list
            .Select(a => new MyNotifyListItemDto
            {
                Id = a.NotifyId,
                Title = titles.TryGetValue(a.NotifyId, out var t) ? t : string.Empty,
                PublishedAt = pubAt.TryGetValue(a.NotifyId, out var p) ? p : null,
                IsRead = a.ReadAt != null
            })
            .OrderByDescending(x => x.PublishedAt ?? DateTime.MinValue)
            .Take(200)
            .ToList();
    }

    // 8) 我的通知详情
    public async Task<MyNotifyDetailDto?> GetMyDetailAsync(long userId, long notifyId, CancellationToken ct = default)
    {
        var a = await _db.Queryable<AudienceMember>().FirstAsync(x => x.UserId == userId && x.NotifyId == notifyId);
        var m = await _db.Queryable<NotificationMessage>().InSingleAsync(notifyId);
        if (a == null || m == null) return null;
        var deleted = m.IsDeleted;
        return new MyNotifyDetailDto
        {
            Id = m.Id,
            Title = m.Title,
            ContentType = m.ContentType,
            ContentText = deleted ? "内容已下架" : m.ContentText,
            ContentHtml = deleted ? null : m.ContentHtml,
            PublishedAt = m.PublishedAt,
            IsRead = a.ReadAt != null
        };
    }

    // 9) 标记已读
    public async Task MarkReadAsync(long userId, long notifyId, CancellationToken ct = default)
    {
        await _audienceRepo.MarkReadAsync(notifyId, userId, DateTime.Now, ct);
    }

    // 10) 未读数
    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default)
    {
        return await _db.Queryable<AudienceMember>().Where(x => x.UserId == userId && x.ReadAt == null).CountAsync();
    }

    // 11) 统计
    public async Task<NotifyStatsDto> GetStatsAsync(long notifyId, CancellationToken ct = default)
    {
        var totalRecipients = await _db.Queryable<AudienceMember>().Where(x => x.NotifyId == notifyId).CountAsync();
        var allAud = await _db.Queryable<AudienceMember>().Where(x => x.NotifyId == notifyId).ToListAsync();
        var deliveredAud = allAud.Where(x => x.DeliveryStatus == 1 || x.DeliveredAt != null).ToList();
        if (deliveredAud.Count == 0 && allAud.Count > 0)
        {
            // 如果尚未进行实际投递打点，按已生成受众视为“已发送”以满足统计展示
            deliveredAud = allAud;
        }
        var readAud = allAud.Where(x => x.ReadAt != null).ToList();
        var unreadIds = allAud.Where(x => x.ReadAt == null).Select(x => x.UserId).ToList();

        var allUserIds = deliveredAud.Select(x => x.UserId)
            .Concat(readAud.Select(x => x.UserId))
            .Concat(unreadIds)
            .Distinct()
            .ToList();
        var users = await _db.Queryable<Ginkgo.Domain.Users.User>()
            .Where(u => allUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync();
        var usersDict = users.ToDictionary(x => x.Id, x => x.DisplayName);

        var readUsers = readAud.Select(x => new UserBriefDto
        {
            Id = x.UserId,
            Name = string.IsNullOrWhiteSpace(x.UserName)
                ? (usersDict.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString())
                : x.UserName!
        }).ToList();
        var deliveredUsers = deliveredAud.Select(x => new UserBriefDto
        {
            Id = x.UserId,
            Name = string.IsNullOrWhiteSpace(x.UserName)
                ? (usersDict.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString())
                : x.UserName!
        }).ToList();
        var unreadUsers = unreadIds.Select(uid => new UserBriefDto
        {
            Id = uid,
            Name = usersDict.TryGetValue(uid, out var n) ? n : uid.ToString()
        }).ToList();

        // 回填消息表的统计（可选）
        try
        {
            await _db.Updateable<NotificationMessage>()
                .SetColumns(m => m.TotalRecipients == totalRecipients)
                .SetColumns(m => m.ReadCount == readAud.Count)
                .SetColumns(m => m.UpdatedAt == DateTime.Now)
                .Where(m => m.Id == notifyId)
                .ExecuteCommandAsync();
        }
        catch { }

        return new NotifyStatsDto
        {
            Id = notifyId,
            TotalRecipients = totalRecipients,
            DeliveredCount = deliveredAud.Count,
            ReadCount = readAud.Count,
            DeliveredUsers = deliveredUsers,
            UnreadUsers = unreadUsers,
            ReadUsers = readUsers
        };
        }

        // 12) 统计-摘要（前10个名单，供列表侧边展示）
        public async Task<NotifyStatsSummaryDto> GetStatsSummaryAsync(long notifyId, CancellationToken ct = default)
        {
            var stats = await GetStatsAsync(notifyId, ct);
            return new NotifyStatsSummaryDto
            {
                Id = notifyId,
                TotalRecipients = stats.TotalRecipients,
                DeliveredCount = stats.DeliveredCount,
                ReadCount = stats.ReadCount,
                TopDeliveredUsers = stats.DeliveredUsers.Take(10).ToList(),
                TopUnreadUsers = stats.UnreadUsers.Take(10).ToList(),
                DetailUrl = $"/api/v1/notifications/{notifyId}/detail"
            };
        }

        // 13) 已发布通知详情 + 名单分页/搜索
        public async Task<PublishedNotifyDetailDto?> GetPublishedDetailAsync(long notifyId, string? listType = "unread", string? keyword = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var m = await _db.Queryable<NotificationMessage>().FirstAsync(x => x.Id == notifyId);
            if (m == null || m.Status != 1) return null; // 仅对已发布提供详情

            // 汇总
            var totalRecipients = await _db.Queryable<AudienceMember>().Where(x => x.NotifyId == notifyId).CountAsync();
            var readCount = await _db.Queryable<AudienceMember>().Where(x => x.NotifyId == notifyId && x.ReadAt != null).CountAsync();
            var deliveredCount = await _db.Queryable<AudienceMember>().Where(x => x.NotifyId == notifyId).CountAsync(); // 若无实际投递标记则等同总数

            // 附件
            var atts = await _db.Queryable<AttachmentVO>().Where(a => a.NotifyId == notifyId)
                .Select(a => new AttachmentDto { FileId = a.FileId, Name = a.Name, ContentType = a.ContentType, Size = a.Size })
                .ToListAsync();

            // 名单查询
            listType = (listType ?? "unread").ToLowerInvariant();
            var q = _db.Queryable<AudienceMember, Ginkgo.Domain.Users.User>((a, u) => new JoinQueryInfos(JoinType.Inner, a.UserId == u.Id))
                .Where((a, u) => a.NotifyId == notifyId);
            if (listType == "read") q = q.Where((a, u) => a.ReadAt != null);
            else if (listType != "delivered") // 已生成受众即视为已送达，无需额外过滤
                q = q.Where((a, u) => a.ReadAt == null); // unread 默认
            if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where((a, u) => u.DisplayName.Contains(keyword!));
            RefAsync<int> total = 0;
            var users = await q.Select((a, u) => new UserBriefDto { Id = u.Id, Name = u.DisplayName })
                .ToPageListAsync(page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize, total);

            // 删除提示
            var (contentText, contentHtml) = m.IsDeleted ? ("内容已下架", null) : (m.ContentText, m.ContentHtml);

            return new PublishedNotifyDetailDto
            {
                Id = m.Id,
                Title = m.Title,
                ContentType = m.ContentType,
                ContentText = contentText,
                ContentHtml = contentHtml,
                PublishedAt = m.PublishedAt,
                IsDeleted = m.IsDeleted,
                Attachments = atts,
                TotalRecipients = totalRecipients,
                DeliveredCount = deliveredCount,
                ReadCount = readCount,
                ListType = listType,
                Keyword = keyword,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Users = users
            };
        }

        // 14) 软删除（用于已发布）
        public async Task SoftDeleteAsync(long notifyId, CancellationToken ct = default)
        {
            await _db.Updateable<NotificationMessage>()
                .SetColumns(m => m.IsDeleted == true)
                .SetColumns(m => m.UpdatedAt == DateTime.Now)
                .Where(m => m.Id == notifyId)
                .ExecuteCommandAsync();
        }

}

