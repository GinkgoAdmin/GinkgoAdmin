using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain;
using Ginkgo.Domain.Roles;

namespace Ginkgo.Infrastructure.Persistence.Services
{
    /// <summary>
    /// 角色数据范围领域服务（基础设施实现）。
    /// 负责读取/替换"指定部门"映射，并在事务内更新角色的数据范围策略。
    /// </summary>
    public sealed class RoleDataScopeService : IRoleDataScopeService
    {
        private readonly IRepository<Role> _roleRepo;
        private readonly IRepository<RoleDataScopeDept> _relRepo;
        private readonly IUnitOfWork _uow;

        public RoleDataScopeService(IRepository<Role> roleRepo,
                                    IRepository<RoleDataScopeDept> relRepo,
                                    IUnitOfWork uow)
        {
            _roleRepo = roleRepo;
            _relRepo = relRepo;
            _uow = uow;
        }

        public Task<IReadOnlyList<long>> GetSpecifiedDepartmentIdsAsync(long roleId, CancellationToken cancellationToken = default)
        {
            var ids = _relRepo.Query()
                .Where(x => x.RoleId == roleId)
                .Select(x => x.DepartmentId)
                .ToList();
            return Task.FromResult((IReadOnlyList<long>)ids);
        }

        public async Task ReplaceAsync(Role role, string normalizedDataScope, IEnumerable<long> departmentIds, CancellationToken cancellationToken = default)
        {
            await _uow.BeginAsync(cancellationToken);
            try
            {
                // 1) 更新角色策略（使用实体领域方法进一步规范化）
                role.SetDataScope(normalizedDataScope);
                await _roleRepo.UpdateAsync(role, cancellationToken);

                // 2) 清空旧映射
                var oldIds = _relRepo.Query().Where(x => x.RoleId == role.Id).Select(x => x.Id).ToList();
                foreach (var id in oldIds)
                {
                    await _relRepo.DeleteAsync(id, cancellationToken);
                }

                // 3) 当策略为"指定部门"时，写入新映射
                if (string.Equals(role.DataScope, "SpecifiedDepartments", StringComparison.Ordinal))
                {
                    var distinctIds = (departmentIds ?? Enumerable.Empty<long>()).Distinct().Where(d => d != 0).ToList();
                    foreach (var depId in distinctIds)
                    {
                        await _relRepo.AddAsync(new RoleDataScopeDept { RoleId = role.Id, DepartmentId = depId }, cancellationToken);
                    }
                }

                await _uow.CommitAsync(cancellationToken);
            }
            catch
            {
                await _uow.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
