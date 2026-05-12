using Ginkgo.Domain.Notifications;
using SqlSugar;
using System.Linq;


namespace Ginkgo.Infrastructure.Persistence.SqlSugar.Notifications;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly ISqlSugarClient _db;
    public NotificationRepository(ISqlSugarClient db) => _db = db;

    public async Task<NotificationMessage?> GetAsync(long id, CancellationToken ct = default)
    {
        return await _db.Queryable<NotificationMessage>().FirstAsync(x => x.Id == id);
    }

    public async Task AddAsync(NotificationMessage message, IEnumerable<AttachmentVO> attachments, IEnumerable<AudienceSeed> seeds, CancellationToken ct = default)
    {
        await _db.Ado.BeginTranAsync();
        try
        {
            await _db.Insertable(message).ExecuteCommandAsync();
            if (attachments != null && attachments.Any())
                await _db.Insertable(attachments.ToList()).ExecuteCommandAsync();
            if (seeds != null && seeds.Any())
                await _db.Insertable(seeds.ToList()).ExecuteCommandAsync();
            await _db.Ado.CommitTranAsync();
        }
        catch
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    public async Task UpdateAsync(NotificationMessage message, CancellationToken ct = default)
    {
        await _db.Updateable(message).ExecuteCommandAsync();
    }

    public async Task<List<AudienceSeed>> GetSeedsAsync(long notifyId, CancellationToken ct = default)
    {
        return await _db.Queryable<AudienceSeed>()
            .Where(s => s.NotifyId == notifyId)
            .ToListAsync();
        }

    public async Task<Dictionary<long, string>> GetTitlesAsync(IEnumerable<long> notifyIds, CancellationToken ct = default)
    {
        var ids = notifyIds?.Where(id => id != 0).Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0) return new Dictionary<long, string>();
        var list = await _db.Queryable<NotificationMessage>()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.Title })
            .ToListAsync();
        return list.ToDictionary(x => x.Id, x => x.Title);
    }

    }
