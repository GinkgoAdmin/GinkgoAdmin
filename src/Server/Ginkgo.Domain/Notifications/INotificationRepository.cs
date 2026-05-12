namespace Ginkgo.Domain.Notifications;

public interface INotificationRepository
{
    Task<NotificationMessage?> GetAsync(long id, CancellationToken ct = default);
    Task AddAsync(NotificationMessage message, IEnumerable<AttachmentVO> attachments, IEnumerable<AudienceSeed> seeds, CancellationToken ct = default);
    Task UpdateAsync(NotificationMessage message, CancellationToken ct = default);
    Task<List<AudienceSeed>> GetSeedsAsync(long notifyId, CancellationToken ct = default);

    // 读取消息标题（用于收件箱展示）
    Task<Dictionary<long, string>> GetTitlesAsync(IEnumerable<long> notifyIds, CancellationToken ct = default);
}

