using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain.Roles;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar
{
    public sealed class RolePermissionRepository : IRolePermissionRepository
    {
        private readonly ISqlSugarClient _db;
        public RolePermissionRepository(ISqlSugarClient db) => _db = db;

        public Task<List<long>> GetAssignedPermissionIdsAsync(long roleId, CancellationToken ct = default)
        {
            return _db.Queryable<RolePermission>()
                .Where(x => x.RoleId == roleId)
                .Select(x => x.PermissionId)
                .ToListAsync();
        }

        public async Task ReplaceAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken ct = default)
        {
            var ids = (permissionIds ?? Enumerable.Empty<long>()).Distinct().Where(x => x != 0).ToList();
            await _db.Ado.BeginTranAsync();
            try
            {
                await _db.Deleteable<RolePermission>().Where(x => x.RoleId == roleId).ExecuteCommandAsync();
                if (ids.Count > 0)
                {
                    var rows = ids.Select(pid => new RolePermission { RoleId = roleId, PermissionId = pid }).ToList();
                    await _db.Insertable(rows).ExecuteCommandAsync();
                }
                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
        }
    }
}
