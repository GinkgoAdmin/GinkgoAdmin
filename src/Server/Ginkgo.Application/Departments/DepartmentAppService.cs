using Ginkgo.Domain;
using Ginkgo.Domain.Departments;
using Ginkgo.Domain.Users;
using Ginkgo.Shared;

namespace Ginkgo.Application.Departments;

public sealed class DepartmentAppService : IDepartmentAppService
{
    private readonly IRepository<Department> _repo;
    private readonly IDepartmentRepository _deptRepo;
    private readonly IUserDepartmentRepository _userDeptRepo;
    private readonly IUserRepository _userRepo;
    public DepartmentAppService(IRepository<Department> repo,
        IDepartmentRepository deptRepo,
        IUserDepartmentRepository userDeptRepo,
        IUserRepository userRepo)
    {
        _repo = repo; _deptRepo = deptRepo; _userDeptRepo = userDeptRepo; _userRepo = userRepo;
    }

    public async Task<PagedResult<DepartmentListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        var (total, list) = await _deptRepo.GetPagedAsync(page, size, keyword, cancellationToken);
        var items = list.Select(x => new DepartmentListItemDto { Id = x.Id, Name = x.Name, Code = x.Code, Enabled = x.Enabled }).ToList();
        return new PagedResult<DepartmentListItemDto>
        {
            Total = total, Page = page, PageSize = size, Items = items
        };
    }

    public async Task<long> CreateAsync(CreateDepartmentInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("部门名称不能为空");
        var e = new Department { Name = input.Name.Trim(), Code = input.Code?.Trim(), ParentId = input.ParentId, Order = 0, Enabled = true };
        await _repo.AddAsync(e, cancellationToken);
        return e.Id;
    }

    public async Task UpdateAsync(long id, UpdateDepartmentInput input, CancellationToken cancellationToken = default)
    {
        var e = await _repo.GetByIdAsync(id, cancellationToken); if (e == null) return;
        if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("部门名称不能为空");
        e.Name = input.Name.Trim(); e.Enabled = input.Enabled; e.Order = input.Order;
        await _repo.UpdateAsync(e, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        // 不允许删除仍有子部门或仍有关联用户的部门
        var all = await _deptRepo.GetAllOrderedAsync(cancellationToken);
        if (all.Any(d => d.ParentId == id)) throw new InvalidOperationException("存在子部门，无法删除");
        var rels = await _userDeptRepo.GetByDepartmentAsync(id, cancellationToken);
        if (rels.Any()) throw new InvalidOperationException("部门下仍有关联用户，无法删除");
        await _repo.DeleteAsync(id, cancellationToken);
    }

    public async Task<List<DepartmentTreeNodeDto>> GetAllTreeAsync(CancellationToken cancellationToken = default)
    {
        var list = await _deptRepo.GetAllOrderedAsync(cancellationToken);
        var dict = list.ToDictionary(x => x.Id, x => new DepartmentTreeNodeDto { Id = x.Id, Name = x.Name });
        var roots = new List<DepartmentTreeNodeDto>();
        foreach (var d in list)
        {
            var node = dict[d.Id];
            if (d.ParentId.HasValue && dict.TryGetValue(d.ParentId.Value, out var parent)) parent.Children.Add(node);
            else roots.Add(node);
        }
        return await Task.FromResult(roots);
    }

    public async Task<DepartmentDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var e = await _repo.GetByIdAsync(id, cancellationToken);
        if (e == null) return null;
        return new DepartmentDetailDto { Id = e.Id, Name = e.Name, Code = e.Code, ParentId = e.ParentId, Enabled = e.Enabled, Order = e.Order };
    }

    public async Task<List<DepartmentUserDto>> GetUsersByDepartmentAsync(long id, CancellationToken cancellationToken = default)
    {
        // SqlSugar 的 ISugarQueryable 不支持 LINQ 的 join 语法，这里改为两步查询并在内存中组装
        var rels = await _userDeptRepo.GetByDepartmentAsync(id, cancellationToken);
        if (rels.Count == 0) return new List<DepartmentUserDto>();
        var userIds = rels.Select(r => r.UserId).ToList();
        var users = await _userRepo.GetByIdsAsync(userIds, cancellationToken);
        var isManagerMap = rels.ToDictionary(r => r.UserId, r => r.IsManager);
        var list = users.Select(u => new DepartmentUserDto
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Phone = u.Phone,
            IsManager = isManagerMap.TryGetValue(u.Id, out var flag) && flag
        }).ToList();

        // 排序：负责人置顶；其余按用户创建时间
        list = list
            .OrderByDescending(x => x.IsManager)
            .ThenByDescending(x => users.FirstOrDefault(u => u.Id == x.Id)?.CreatedAt ?? DateTime.MinValue)
            .ToList();
        return list;
    }

    public async Task RemoveUserAsync(long departmentId, long userId, CancellationToken cancellationToken = default)
    {
        await _userDeptRepo.RemoveAsync(departmentId, userId, cancellationToken);
    }

    public async Task SetManagerAsync(long departmentId, long userId, bool isManager, CancellationToken cancellationToken = default)
    {
        await _userDeptRepo.SetManagerAsync(departmentId, userId, isManager, cancellationToken);
    }
}




