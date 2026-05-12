namespace Ginkgo.Domain.Notifications;

public interface IAudienceRepository
{
    Task AddRangeAsync(IEnumerable<AudienceMember> members, CancellationToken ct = default);
    Task<int> MarkReadAsync(long notifyId, long userId, DateTime utcNow, CancellationToken ct = default);
    Task<List<AudienceMember>> GetInboxAsync(long userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);
}

