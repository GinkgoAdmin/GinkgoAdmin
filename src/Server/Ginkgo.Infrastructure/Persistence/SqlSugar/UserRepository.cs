using Ginkgo.Domain.Departments;
using Ginkgo.Domain.Users;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly ISqlSugarClient _db;
        public UserRepository(ISqlSugarClient db) => _db = db;

        public async Task<(long total, List<User> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
        {
            var q = _db.Queryable<User>();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(x => x.UserName.Contains(k) || x.DisplayName.Contains(k));
            }
            q = q.OrderBy(x => x.Id, OrderByType.Desc);
            RefAsync<int> total = 0;
            var list = await q.ToPageListAsync(page, pageSize, total);
            return (total, list);
        }

        public async Task<(long total, List<User> items)> SearchAsync(UserQueryFilter filter, int page, int pageSize, CancellationToken ct = default)
        {
            var q = _db.Queryable<User>();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var k = filter.Keyword.Trim();
                q = q.Where(x => x.UserName.Contains(k) || x.DisplayName.Contains(k));
            }
            if (filter.Enabled.HasValue)
            {
                var en = filter.Enabled.Value;
                q = q.Where(x => x.Enabled == en);
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

            // 部门过滤（可选递归）
            if (filter.DepartmentId.HasValue)
            {
                var deptId = filter.DepartmentId.Value;
                List<long> deptIds = new() { deptId };
                if (filter.DepartmentDeep)
                {
                    // 获取所有部门，在内存中计算子孙节点
                    var allDepts = await _db.Queryable<Department>()
                        .Select(d => new { d.Id, d.ParentId })
                        .ToListAsync();
                    var dict = allDepts.GroupBy(d => d.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
                    var stack = new Stack<long>();
                    stack.Push(deptId);
                    while (stack.Count > 0)
                    {
                        var cur = stack.Pop();
                        if (dict.TryGetValue(cur, out var children))
                        {
                            foreach (var c in children)
                            {
                                if (!deptIds.Contains(c))
                                {
                                    deptIds.Add(c);
                                    stack.Push(c);
                                }
                            }
                        }
                    }
                }
                // 关联 UserDepartment 过滤
                q = q.InnerJoin<UserDepartment>((u, ud) => u.Id == ud.UserId)
                     .Where((u, ud) => deptIds.Contains(ud.DepartmentId))
                     .Distinct();
            }

            q = q.OrderBy(u => u.Id, OrderByType.Desc);
            RefAsync<int> total = 0;
            var list = await q.ToPageListAsync(page, pageSize, total);
            return (total, list);
        }

        public Task<List<User>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default)
        {
            var set = ids?.ToList() ?? new List<long>();
            if (set.Count == 0) return Task.FromResult(new List<User>());
            return _db.Queryable<User>().Where(x => set.Contains(x.Id)).ToListAsync();
        }

        public async Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
        {
            var u = (userName ?? string.Empty).Trim();
            return await _db.Queryable<User>().Where(x => x.UserName == u).FirstAsync();
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            var e = (email ?? string.Empty).Trim();
            return await _db.Queryable<User>().Where(x => x.Email == e).FirstAsync();
        }

        public Task<bool> ExistsByUserNameAsync(string userName, CancellationToken ct = default)
        {
            var u = (userName ?? string.Empty).Trim();
            return _db.Queryable<User>().AnyAsync(x => x.UserName == u);
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        {
            var e = (email ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(e)) return Task.FromResult(false);
            return _db.Queryable<User>().AnyAsync(x => x.Email == e);
        }

        public async Task HardDeleteAsync(long userId, CancellationToken ct = default)
        {
            await _db.Ado.BeginTranAsync();
            try
            {
                // 删除用户角色关联
                await _db.Deleteable<UserRole>().Where(x => x.UserId == userId).ExecuteCommandAsync();
                // 删除用户部门关联
                await _db.Deleteable<UserDepartment>().Where(x => x.UserId == userId).ExecuteCommandAsync();
                // 物理删除用户
                await _db.Deleteable<User>().Where(x => x.Id == userId).ExecuteCommandAsync();
                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
        }
    }
}
