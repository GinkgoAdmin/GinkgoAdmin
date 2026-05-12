using Ginkgo.Domain.Users;
using SqlSugar;
using System.Linq;


namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class UserDepartmentRepository : IUserDepartmentRepository
    {
        private readonly ISqlSugarClient _db;
        public UserDepartmentRepository(ISqlSugarClient db) => _db = db;

        public Task<List<long>> GetDepartmentIdsAsync(long userId, CancellationToken ct = default)
        {
            return _db.Queryable<UserDepartment>()
                .Where(x => x.UserId == userId)
                .Select(x => x.DepartmentId)
                .ToListAsync();
        }

        public async Task ReplaceAsync(long userId, IEnumerable<long> departmentIds, CancellationToken ct = default)
        {
            var ids = (departmentIds ?? Enumerable.Empty<long>()).Distinct().Where(x => x != 0).ToList();
            await _db.Ado.BeginTranAsync();
            try
            {
                await _db.Deleteable<UserDepartment>().Where(x => x.UserId == userId).ExecuteCommandAsync();
                if (ids.Count > 0)
                {
                    var rows = ids.Select(did => new UserDepartment { UserId = userId, DepartmentId = did }).ToList();
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

        public Task<List<UserDepartment>> GetByDepartmentAsync(long departmentId, CancellationToken ct = default)
        {
            return _db.Queryable<UserDepartment>()
                .Where(x => x.DepartmentId == departmentId)
                .ToListAsync();
        }

        public async Task RemoveAsync(long departmentId, long userId, CancellationToken ct = default)
        {
            await _db.Deleteable<UserDepartment>()
                .Where(x => x.DepartmentId == departmentId && x.UserId == userId)
                .ExecuteCommandAsync();
        }

        public async Task SetManagerAsync(long departmentId, long userId, bool isManager, CancellationToken ct = default)
        {
            var rel = await _db.Queryable<UserDepartment>()
                .Where(x => x.DepartmentId == departmentId && x.UserId == userId)
                .FirstAsync();
            if (rel == null) return;
            rel.IsManager = isManager;
            await _db.Updateable(rel).ExecuteCommandAsync();
        }

        public Task<List<long>> GetUserIdsByDepartmentIdsAsync(IEnumerable<long> departmentIds, CancellationToken ct = default)
        {
            var ids = departmentIds?.Where(id => id != 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0) return Task.FromResult(new List<long>());
            return _db.Queryable<UserDepartment>()
                .Where(x => ids.Contains(x.DepartmentId))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();
        }
    }
}
