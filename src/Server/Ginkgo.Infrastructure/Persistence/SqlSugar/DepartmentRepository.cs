using Ginkgo.Domain.Departments;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class DepartmentRepository : IDepartmentRepository
    {
        private readonly ISqlSugarClient _db;
        public DepartmentRepository(ISqlSugarClient db) => _db = db;

        public async Task<(long total, List<Department> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
        {
            var q = _db.Queryable<Department>()
                .Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(x => x.Name.Contains(k) || (x.Code ?? "").Contains(k));
            }
            q = q.OrderBy(x => x.ParentId).OrderBy(x => x.Order);
            RefAsync<int> total = 0;
            var list = await q.ToPageListAsync(page, pageSize, total);
            return (total, list);
        }

        public Task<List<Department>> GetAllOrderedAsync(CancellationToken ct = default)
        {
            return _db.Queryable<Department>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.ParentId)
                .OrderBy(x => x.Order)
                .ToListAsync();
        }

        public async Task<List<long>> GetDescendantIdsAsync(long parentId, bool includeSelf = true, CancellationToken ct = default)
        {
            var all = await _db.Queryable<Department>()
                .Where(x => !x.IsDeleted)
                .Select(d => new { d.Id, d.ParentId })
                .ToListAsync();
            var childrenMap = all.GroupBy(d => d.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
            var result = new List<long>();
            if (includeSelf) result.Add(parentId);
            var stack = new Stack<long>();
            stack.Push(parentId);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (childrenMap.TryGetValue(cur, out var kids))
                {
                    foreach (var c in kids)
                    {
                        if (!result.Contains(c))
                        {
                            result.Add(c);
                            stack.Push(c);
                        }
                    }
                }
            }
            return result;
        }

        public async Task<(long total, List<Department> items)> SearchAsync(DepartmentQueryFilter filter, int page, int pageSize, CancellationToken ct = default)
        {
            var q = _db.Queryable<Department>()
                .Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var k = filter.Keyword.Trim();
                q = q.Where(x => x.Name.Contains(k) || (x.Code ?? "").Contains(k));
            }
            if (filter.Enabled.HasValue)
            {
                q = q.Where(x => x.Enabled == filter.Enabled.Value);
            }
            if (filter.CreatedFrom.HasValue)
            {
                var from = filter.CreatedFrom.Value.UtcDateTime;
                q = q.Where(x => x.CreatedAt >= from);
            }
            if (filter.CreatedTo.HasValue)
            {
                var to = filter.CreatedTo.Value.UtcDateTime;
                q = q.Where(x => x.CreatedAt <= to);
            }
            if (filter.ParentId.HasValue)
            {
                if (filter.ParentDeep)
                {
                    var ids = await GetDescendantIdsAsync(filter.ParentId.Value, includeSelf: true, ct);
                    q = q.Where(d => ids.Contains(d.Id));
                }
                else
                {
                    var pid = filter.ParentId.Value;
                    q = q.Where(d => d.ParentId == pid);
                }
            }

            q = q.OrderBy(x => x.ParentId).OrderBy(x => x.Order);
            RefAsync<int> total = 0;
            var list = await q.ToPageListAsync(page, pageSize, total);
            return (total, list);
        }
    }
}

