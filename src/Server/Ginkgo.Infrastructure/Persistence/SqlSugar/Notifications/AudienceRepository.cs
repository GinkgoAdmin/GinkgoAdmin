using Ginkgo.Domain.Notifications;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar.Notifications;

public sealed class AudienceRepository : IAudienceRepository
{
    private readonly ISqlSugarClient _db;
    public AudienceRepository(ISqlSugarClient db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<AudienceMember> members, CancellationToken ct = default)
    {
        if (members == null) return;
        var list = members.ToList();
        if (list.Count == 0) return;
        // 去重插入，避免 UX_Notify_Audience_Notify_User 唯一索引冲突
        var groups = list.GroupBy(m => m.NotifyId);
        foreach (var g in groups)
        {
            var notifyId = g.Key;
            var existingUserIds = await _db.Queryable<AudienceMember>()
                .Where(x => x.NotifyId == notifyId)
                .Select(x => x.UserId)
                .ToListAsync();
            var existSet = new HashSet<long>(existingUserIds);
            var toInsert = g.Where(m => !existSet.Contains(m.UserId))
                .GroupBy(m => m.UserId)
                .Select(grp => grp.First())
                .ToList();
            if (toInsert.Count > 0)
            {
                await _db.Insertable(toInsert).ExecuteCommandAsync();
            }
        }
    }

    public async Task<int> MarkReadAsync(long notifyId, long userId, DateTime utcNow, CancellationToken ct = default)
    {
        return await _db.Updateable<AudienceMember>()
            .SetColumns(a => a.ReadAt == utcNow)
            .Where(a => a.NotifyId == notifyId && a.UserId == userId && a.ReadAt == null)
            .ExecuteCommandAsync();
    }

    public async Task<List<AudienceMember>> GetInboxAsync(long userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Queryable<AudienceMember>().Where(a => a.UserId == userId);
        if (unreadOnly) q = q.Where(a => a.ReadAt == null);
        return await q.OrderBy(a => a.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(page, pageSize);
    }
}
