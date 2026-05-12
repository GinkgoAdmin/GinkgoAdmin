using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Roles
{
    /// <summary>
    /// 角色仓储契约（领域层）。
    /// </summary>
    public interface IRoleRepository
    {
        /// <summary>
        /// 分页查询（按关键词模糊匹配 Name/Code，按 Id 倒序）。
        /// 返回 total 与 items。
        /// </summary>
        Task<(long total, List<Role> items)> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);

        /// <summary>
        /// 获取扁平列表（用于角色树构建），按 ParentId、Name 排序。
        /// </summary>
        Task<List<Role>> GetAllOrderedAsync(CancellationToken ct = default);
    }
}

