using Ginkgo.Domain.Departments;

namespace Ginkgo.Domain.Departments;

public interface IDepartmentRepository
{
    Task<(long total, List<Department> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<List<Department>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<List<long>> GetDescendantIdsAsync(long parentId, bool includeSelf = true, CancellationToken ct = default);
    Task<(long total, List<Department> items)> SearchAsync(DepartmentQueryFilter filter, int page, int pageSize, CancellationToken ct = default);
}

