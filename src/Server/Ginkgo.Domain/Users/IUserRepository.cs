using Ginkgo.Domain.Users;

namespace Ginkgo.Domain.Users;

public interface IUserRepository
{
    Task<(long total, List<User> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<(long total, List<User> items)> SearchAsync(UserQueryFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<List<User>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default);

    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 硬删除用户（物理删除，同时删除关联的角色和部门关系）。
    /// </summary>
    Task HardDeleteAsync(long userId, CancellationToken ct = default);
}

