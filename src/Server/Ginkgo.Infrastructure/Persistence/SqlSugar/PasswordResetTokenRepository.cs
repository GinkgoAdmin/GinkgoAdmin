using Ginkgo.Domain.Users;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly ISqlSugarClient _db;
        public PasswordResetTokenRepository(ISqlSugarClient db) => _db = db;

        public Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
        {
            return _db.Insertable(token).ExecuteCommandAsync();
        }

        public async Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        {
            var h = (tokenHash ?? string.Empty).Trim();
            return await _db.Queryable<PasswordResetToken>().Where(x => x.TokenHash == h).FirstAsync();
        }

        public Task MarkUsedAsync(long id, DateTime usedAtUtc, CancellationToken ct = default)
        {
            return _db.Updateable<PasswordResetToken>()
                .SetColumns(x => new PasswordResetToken { UsedAt = usedAtUtc })
                .Where(x => x.Id == id)
                .ExecuteCommandAsync();
        }

        public Task DeleteExpiredAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            return _db.Deleteable<PasswordResetToken>().Where(x => x.ExpiresAt < nowUtc || x.UsedAt != null).ExecuteCommandAsync();
        }
    }
}
