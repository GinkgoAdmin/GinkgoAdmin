using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain.Permissions;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class PermissionRepository : IPermissionRepository
    {
        private readonly ISqlSugarClient _db;
        public PermissionRepository(ISqlSugarClient db) => _db = db;

        public Task<List<Permission>> GetAllEnabledAsync(CancellationToken ct = default)
        {
            return _db.Queryable<Permission>()
                .Where(x => x.Enabled)
                .OrderBy(x => x.Code)
                .ToListAsync();
        }
    }
}

