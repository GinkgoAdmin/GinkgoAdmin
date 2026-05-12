using Ginkgo.Shared;

namespace Ginkgo.Application.Departments;

public interface IDepartmentAppService
{
    Task<PagedResult<DepartmentListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default);
    Task<List<DepartmentTreeNodeDto>> GetAllTreeAsync(CancellationToken cancellationToken = default);
    Task<DepartmentDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<List<DepartmentUserDto>> GetUsersByDepartmentAsync(long id, CancellationToken cancellationToken = default);
    Task RemoveUserAsync(long departmentId, long userId, CancellationToken cancellationToken = default);
    Task SetManagerAsync(long departmentId, long userId, bool isManager, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(CreateDepartmentInput input, CancellationToken cancellationToken = default);
    Task UpdateAsync(long id, UpdateDepartmentInput input, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}




