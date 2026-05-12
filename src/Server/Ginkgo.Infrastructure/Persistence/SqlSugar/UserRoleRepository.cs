using Ginkgo.Domain.Users;
using SqlSugar;
using System.Linq;


namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class UserRoleRepository : IUserRoleRepository
    {
        private readonly ISqlSugarClient _db;
        public UserRoleRepository(ISqlSugarClient db) => _db = db;

        public Task<List<long>> GetRoleIdsAsync(long userId, CancellationToken ct = default)
        {
            return _db.Queryable<UserRole>()
                .Where(x => x.UserId == userId)
                .Select(x => x.RoleId)
                .ToListAsync();
        }

        public async Task ReplaceAsync(long userId, IEnumerable<long> roleIds, CancellationToken ct = default)
        {
            var ids = (roleIds ?? Enumerable.Empty<long>()).Distinct().Where(x => x != 0).ToList();
            await _db.Ado.BeginTranAsync();
            try
            {
                await _db.Deleteable<UserRole>().Where(x => x.UserId == userId).ExecuteCommandAsync();
                if (ids.Count > 0)
                {
                    var rows = ids.Select(rid => new UserRole { UserId = userId, RoleId = rid }).ToList();
                    await _db.Insertable(rows).ExecuteCommandAsync();
                }
                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
        }

        public Task<List<long>> GetUserIdsByRoleIdsAsync(IEnumerable<long> roleIds, CancellationToken ct = default)
        {
            var ids = roleIds?.Where(id => id != 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0) return Task.FromResult(new List<long>());
            return _db.Queryable<UserRole>()
                .Where(x => ids.Contains(x.RoleId))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();
        }
    }
}
