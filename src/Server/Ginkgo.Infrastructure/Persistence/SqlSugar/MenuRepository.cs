using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain.Menus;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class MenuRepository : IMenuRepository
    {
        private readonly ISqlSugarClient _db;
        public MenuRepository(ISqlSugarClient db) => _db = db;

        public Task<List<Menu>> GetAllOrderedAsync(CancellationToken ct = default)
        {
            return _db.Queryable<Menu>()
                .OrderBy(x => x.ParentId)
                .OrderBy(x => x.Order)
                .ToListAsync();
        }
    }
}

