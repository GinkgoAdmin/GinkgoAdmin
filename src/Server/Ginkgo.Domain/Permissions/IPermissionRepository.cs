using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Permissions
{
    /// <summary>
    /// 权限仓储契约。
    /// </summary>
    public interface IPermissionRepository
    {
        /// <summary>
        /// 获取所有启用的权限，按 Code 排序。
        /// </summary>
        Task<List<Permission>> GetAllEnabledAsync(CancellationToken ct = default);
    }
}

