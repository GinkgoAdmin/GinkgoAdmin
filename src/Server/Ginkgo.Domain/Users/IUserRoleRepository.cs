using Ginkgo.Domain.Users;

namespace Ginkgo.Domain.Users;

public interface IUserRoleRepository
{
    Task<List<long>> GetRoleIdsAsync(long userId, CancellationToken ct = default);
    Task ReplaceAsync(long userId, IEnumerable<long> roleIds, CancellationToken ct = default);

    // 批量：根据角色Id集合获取用户Id集合
    Task<List<long>> GetUserIdsByRoleIdsAsync(IEnumerable<long> roleIds, CancellationToken ct = default);
}

