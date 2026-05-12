using Ginkgo.Domain;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Departments;
using Ginkgo.Domain.Settings;
 

namespace Ginkgo.Application;

public interface IDataPermissionService
{
    Task<HashSet<long>> GetAccessibleDepartmentIdsAsync(long userId, CancellationToken ct = default);
}

public sealed class DataPermissionService : IDataPermissionService
{
    private readonly IRepository<UserDepartment> _userDeptRepo;
    private readonly IRepository<Department> _deptRepo;
    
    public DataPermissionService(IRepository<UserDepartment> userDeptRepo,
                                 IRepository<Department> deptRepo)
    {
        _userDeptRepo = userDeptRepo;
        _deptRepo = deptRepo;
    }

    public async Task<HashSet<long>> GetAccessibleDepartmentIdsAsync(long userId, CancellationToken ct = default)
    {
        // 取用户所属部门集合
        var myDeptIds = _userDeptRepo.Query().Where(x => x.UserId == userId).Select(x => x.DepartmentId).ToList();
        var result = new HashSet<long>(myDeptIds);
        if (result.Count == 0) return result;

        // 加载全部部门（邻接表），在内存计算后代集合
        var all = _deptRepo.Query().Select(d => new { d.Id, d.ParentId }).ToList();
        // 由于 GroupBy 键为 long?，不能直接作为字典键（notnull 约束）。这里将 null 合并到 0 键。
        var parentToChildren = all
            .GroupBy(x => x.ParentId)
            .ToDictionary(g => g.Key ?? 0L, g => g.Select(y => y.Id).ToList());

        var queue = new Queue<long>(result);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (parentToChildren.TryGetValue(id, out var children))
            {
                foreach (var c in children)
                {
                    if (result.Add(c)) queue.Enqueue(c);
                }
            }
        }
        return await Task.FromResult(result);
    }
}


