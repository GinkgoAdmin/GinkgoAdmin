using Ginkgo.Domain.Notifications;

namespace Ginkgo.Application.Notifications;

public interface INotificationAppService
{
    Task<long> CreateAsync(CreateNotificationDto input, long? operatorId = null, CancellationToken ct = default);
    Task PublishAsync(long id, long? operatorId = null, CancellationToken ct = default);
    Task MarkAsReadAsync(long id, long userId, CancellationToken ct = default);
    Task<List<InboxItemDto>> GetInboxAsync(long userId, bool unreadOnly, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public sealed class NotificationAppService : INotificationAppService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IAudienceRepository _audienceRepo;
    private readonly IAudienceResolver _resolver;

    public NotificationAppService(INotificationRepository notificationRepo, IAudienceRepository audienceRepo, IAudienceResolver resolver)
    {
        _notificationRepo = notificationRepo;
        _audienceRepo = audienceRepo;
        _resolver = resolver;
    }

    public async Task<long> CreateAsync(CreateNotificationDto input, long? operatorId = null, CancellationToken ct = default)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var msg = NotificationMessage.Create(input.Title, input.ContentType, input.ContentText, input.ContentHtml,
            input.IsImportant, input.Priority, operatorId, null, input.ScheduledAt);

        var seeds = input.Seeds?.Select(s => AudienceSeed.Create(msg.Id, s.TargetType, s.TargetValue)) ?? Enumerable.Empty<AudienceSeed>();
        var atts = input.Attachments?.Select(a => AttachmentVO.Create(msg.Id, a.FileId, a.Name, a.ContentType, a.Size)) ?? Enumerable.Empty<AttachmentVO>();

        await _notificationRepo.AddAsync(msg, atts, seeds, ct);
        return msg.Id;
    }

    public async Task PublishAsync(long id, long? operatorId = null, CancellationToken ct = default)
    {
        var msg = await _notificationRepo.GetAsync(id, ct) ?? throw new InvalidOperationException("通知消息不存在");
        msg.Publish(DateTime.Now);
        var seeds = await _notificationRepo.GetSeedsAsync(id, ct);
        var members = await _resolver.ResolveAsync(id, seeds, ct);
        if (members.Count > 0)
        {
            await _audienceRepo.AddRangeAsync(members, ct);
            msg.IncreaseTotalRecipients(members.Count);
        }

        await _notificationRepo.UpdateAsync(msg, ct);
        // 受众解析：最小实现（仅支持用户ID种子）；后续扩展角色/部门/表达式
    }

    public async Task MarkAsReadAsync(long id, long userId, CancellationToken ct = default)
    {
        if (userId == 0) throw new ArgumentException("userId");
        _ = await _audienceRepo.MarkReadAsync(id, userId, DateTime.Now, ct);
    }

    public async Task<List<InboxItemDto>> GetInboxAsync(long userId, bool unreadOnly, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var list = await _audienceRepo.GetInboxAsync(userId, unreadOnly, page, pageSize, ct);
        var ids = list.Select(a => a.NotifyId).Distinct().ToList();
        var titles = await _notificationRepo.GetTitlesAsync(ids, ct);
        return list.Select(a => new InboxItemDto
        {
            NotifyId = a.NotifyId,
            Title = (titles.TryGetValue(a.NotifyId, out var t) ? t : string.Empty),
            IsRead = a.ReadAt != null,
            CreatedAt = a.CreatedAt,
            DeliveredAt = a.DeliveredAt,
            ReadAt = a.ReadAt
        }).ToList();
    }
}

