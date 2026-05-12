using Ginkgo.Domain.Users;

namespace Ginkgo.Domain.Users;

public interface IUserDepartmentRepository
{
    Task<List<long>> GetDepartmentIdsAsync(long userId, CancellationToken ct = default);
    Task ReplaceAsync(long userId, IEnumerable<long> departmentIds, CancellationToken ct = default);
    Task<List<UserDepartment>> GetByDepartmentAsync(long departmentId, CancellationToken ct = default);
    Task RemoveAsync(long departmentId, long userId, CancellationToken ct = default);
    Task SetManagerAsync(long departmentId, long userId, bool isManager, CancellationToken ct = default);

    // 批量：根据部门Id集合获取用户Id集合
    Task<List<long>> GetUserIdsByDepartmentIdsAsync(IEnumerable<long> departmentIds, CancellationToken ct = default);
}

