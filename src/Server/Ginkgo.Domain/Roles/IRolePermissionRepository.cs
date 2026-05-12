using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Roles
{
    /// <summary>
    /// 角色-权限关系仓储契约。
    /// </summary>
    public interface IRolePermissionRepository
    {
        /// <summary>
        /// 获取角色已分配的权限（此处 PermissionId 存储 MenuId）。
        /// </summary>
        Task<List<long>> GetAssignedPermissionIdsAsync(long roleId, CancellationToken ct = default);

        /// <summary>
        /// 以替换方式保存角色的权限（先删后插，内部进行最小化写入）。
        /// </summary>
        Task ReplaceAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken ct = default);
    }
}

