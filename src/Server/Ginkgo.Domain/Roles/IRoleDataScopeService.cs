using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Roles
{
    /// <summary>
    /// 角色数据范围领域服务（领域层接口）。
    /// 负责读取"指定部门"映射与以事务方式替换数据范围设置。
    /// 具体数据访问由基础设施层实现，应用层只负责编排与 DTO 映射。
    /// </summary>
    public interface IRoleDataScopeService
    {
        /// <summary>
        /// 获取角色在"指定部门（SpecifiedDepartments）"策略下的部门 Id 列表。
        /// </summary>
        Task<IReadOnlyList<long>> GetSpecifiedDepartmentIdsAsync(long roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 以事务方式替换角色的数据范围设置：更新角色实体的策略字段，并替换"指定部门"映射。
        /// 说明：如果 <paramref name="normalizedDataScope"/> 不为 "SpecifiedDepartments"，则会清空映射。
        /// </summary>
        Task ReplaceAsync(Role role, string normalizedDataScope, IEnumerable<long> departmentIds, CancellationToken cancellationToken = default);
    }
}
