using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain.Roles;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class RoleRepository : IRoleRepository
    {
        private readonly ISqlSugarClient _db;
        public RoleRepository(ISqlSugarClient db) => _db = db;

        public async Task<(long total, List<Role> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            var q = _db.Queryable<Role>().Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(x => x.Name.Contains(k) || x.Code.Contains(k));
            }
            q = q.OrderBy(x => x.Id, OrderByType.Desc);
            RefAsync<int> total = 0;
            var list = await q.ToPageListAsync(page, pageSize, total);
            return (total, list);
        }

        public Task<List<Role>> GetAllOrderedAsync(CancellationToken ct = default)
        {
            return _db.Queryable<Role>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.ParentId == null ? 0 : 1)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }
    }
}

