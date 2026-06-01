// 文件功能说明：
// 角色应用服务：分页、增删改，以及权限树/角色树与权限分配（占位实现）。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Permissions;
using Ginkgo.Domain.Roles;
using Ginkgo.Shared;

namespace Ginkgo.Application.Roles;

/// <summary>
/// 角色应用服务实现（占位）。
/// </summary>
public sealed class RoleAppService : IRoleAppService
{
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<Permission> _permissionRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<RoleMenuGroupItem> _roleMenuGroupItemRepository;
    private readonly IRoleDataScopeService _roleDataScopeService;

    // 专用仓储/服务（下沉复杂查询，避免应用层直接 Query）
    private readonly IRoleRepository _roleRepoEx;
    private readonly IRolePermissionRepository _rolePermRepoEx;
    private readonly IMenuRepository _menuRepoEx;
    private readonly IPermissionRepository _permRepoEx;


    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="roleRepository">角色仓储。</param>
    /// <param name="permissionRepository">权限仓储。</param>
    /// <param name="rolePermissionRepository">角色-权限关系仓储。</param>
    /// <param name="menuRepository">菜单仓储。</param>
    /// <param name="roleMenuGroupItemRepository">角色-菜单组项授权关系仓储（删除角色时级联清理）。</param>
    /// <param name="roleDataScopeService">角色数据范围领域服务。</param>
    /// <param name="roleRepoEx">角色仓储（查询/分页专用）。</param>
    /// <param name="rolePermRepoEx">角色-权限关系仓储（替换式保存）。</param>
    /// <param name="menuRepoEx">菜单仓储（用于权限树构建）。</param>
    /// <param name="permRepoEx">权限仓储（用于权限列表）。</param>

    public RoleAppService(
        IRepository<Role> roleRepository,
        IRepository<Permission> permissionRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IRepository<Menu> menuRepository,
        IRepository<RoleMenuGroupItem> roleMenuGroupItemRepository,
        IRoleDataScopeService roleDataScopeService,
        IRoleRepository roleRepoEx,
        IRolePermissionRepository rolePermRepoEx,
        IMenuRepository menuRepoEx,
        IPermissionRepository permRepoEx)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _menuRepository = menuRepository;
        _roleMenuGroupItemRepository = roleMenuGroupItemRepository;
        _roleDataScopeService = roleDataScopeService;
        _roleRepoEx = roleRepoEx;
        _rolePermRepoEx = rolePermRepoEx;
        _menuRepoEx = menuRepoEx;
        _permRepoEx = permRepoEx;
    }

    /// <inheritdoc />
    public async Task<PagedResult<RoleListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;

        var (total, roles) = await _roleRepoEx.GetPagedAsync(page, size, keyword, cancellationToken);
        var items = roles.Select(x => new RoleListItemDto
        {
            Id = x.Id,
            Name = x.Name,
            Code = x.Code,
            Enabled = x.Enabled,
            DataScope = x.DataScope,
            AllowedClients = x.AllowedClients,
            IsSuperAdmin = x.IsSuperAdmin
        }).ToList();
        return new PagedResult<RoleListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CreateRoleInput input, CancellationToken cancellationToken = default)
    {
        var entity = new Role
        {
            Name = input.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(input.Code) ? string.Empty : input.Code.Trim(),
            Enabled = input.Enabled,
            ParentId = input.ParentId,
            AllowedClients = string.IsNullOrWhiteSpace(input.AllowedClients) ? null : input.AllowedClients.Trim(),
            IsSuperAdmin = input.IsSuperAdmin
        };
        entity.SetDataScope(input.DataScope);
        await _roleRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, UpdateRoleInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return;
        entity.Name = input.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(input.Code) ? string.Empty : input.Code.Trim();
        entity.Enabled = input.Enabled;
        entity.ParentId = input.ParentId;
        entity.AllowedClients = string.IsNullOrWhiteSpace(input.AllowedClients) ? null : input.AllowedClients.Trim();
        entity.IsSuperAdmin = input.IsSuperAdmin;
        entity.SetDataScope(input.DataScope);
        await _roleRepository.UpdateAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        // 级联清理该角色的菜单组项（item 级）授权关联（RoleMenuGroupItem），避免残留孤儿授权（需求 8.7）
        var roleItemIds = _roleMenuGroupItemRepository.Query()
            .Where(x => x.RoleId == id)
            .Select(x => x.Id)
            .ToList();
        if (roleItemIds.Count > 0)
        {
            await _roleMenuGroupItemRepository.DeleteRangeAsync(roleItemIds, cancellationToken);
        }

        await _roleRepository.DeleteAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PermissionItemDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var list = (await _permRepoEx.GetAllEnabledAsync(cancellationToken))
            .Select(x => new PermissionItemDto { Id = x.Id, Code = x.Code, Name = x.Name, Type = x.Type })
            .ToList();
        return list;
    }

    /// <inheritdoc />
    public async Task<List<long>> GetRolePermissionIdsAsync(long roleId, CancellationToken cancellationToken = default)
    {
        // 返回该角色已勾选的菜单 Id（即关系表中的 PermissionId 存储 MenuId）
        var ids = await _rolePermRepoEx.GetAssignedPermissionIdsAsync(roleId, cancellationToken);
        return ids;
    }

    /// <inheritdoc />
    public async Task SaveRolePermissionsAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken cancellationToken = default)
    {
        await _rolePermRepoEx.ReplaceAsync(roleId, permissionIds ?? Enumerable.Empty<long>(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PermissionTreeNodeDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _menuRepoEx.GetAllOrderedAsync(cancellationToken);

        // 使用菜单 Id 作为分配单位：仅 Button/Api 节点携带可分配的标识（PermissionId 字段承载 MenuId）
        var dict = new Dictionary<long, PermissionTreeNodeDto>();
        foreach (var m in menus)
        {
            var node = new PermissionTreeNodeDto
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                Route = m.Route ?? string.Empty,
                Code = m.Code,
                Resource = m.Resource,
                Method = m.Method,
                // 采用“非目录即可分配”的策略：Directory=null，其余类型均可分配（Item/Menu/Button/Api）
                PermissionId = string.Equals(m.Type, "Directory", StringComparison.OrdinalIgnoreCase)
                               ? (long?)null
                               : m.Id
            };
            dict[m.Id] = node;
        }
        var roots = new List<PermissionTreeNodeDto>();
        foreach (var m in menus)
        {
            var node = dict[m.Id];
            if (m.ParentId.HasValue && dict.TryGetValue(m.ParentId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        return await Task.FromResult(roots);
    }

    /// <inheritdoc />
    public async Task<List<RoleTreeNodeDto>> GetRoleTreeAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepoEx.GetAllOrderedAsync(cancellationToken);
        var dict = roles.ToDictionary(x => x.Id, x => new RoleTreeNodeDto
        {
            Id = x.Id,
            Name = x.Name,
            Code = x.Code,
            Enabled = x.Enabled,
            DataScope = x.DataScope,
            AllowedClients = x.AllowedClients,
            IsSuperAdmin = x.IsSuperAdmin,
            Children = new List<RoleTreeNodeDto>()
        });
        var roots = new List<RoleTreeNodeDto>();
        foreach (var r in roles)
        {
            var node = dict[r.Id];
            if (r.ParentId.HasValue && dict.TryGetValue(r.ParentId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return await Task.FromResult(roots);
    }

    /// <inheritdoc />
    public async Task<RoleDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role == null) return null;
        return new RoleDetailDto
        {
            Id = role.Id,
            Name = role.Name,
            Code = role.Code,
            Enabled = role.Enabled,
            ParentId = role.ParentId,
            AllowedClients = role.AllowedClients,
            IsSuperAdmin = role.IsSuperAdmin
        };
    }

    /// <summary>
    /// 获取角色数据范围设置（策略 + 指定部门列表）。
    /// </summary>
    public async Task<RoleDataScopeDto> GetDataScopeAsync(long roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        var dataScope = role?.DataScope ?? "OwnOnly";
        var deptIds = await _roleDataScopeService.GetSpecifiedDepartmentIdsAsync(roleId, cancellationToken);
        return new RoleDataScopeDto { DataScope = dataScope, DepartmentIds = deptIds.ToList() };
    }

    /// <summary>
    /// 设置角色数据范围（当策略为 SpecifiedDepartments 时需要提供部门列表）。
    /// </summary>
    public async Task SetDataScopeAsync(long roleId, SetRoleDataScopeInput input, CancellationToken cancellationToken = default)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var allowed = new[] { "All", "OwnOnly", "DepartmentOnly", "DepartmentAndChildren", "SpecifiedDepartments", "Custom" };
        var val = input.DataScope?.Trim() ?? "All";
        var normalized = allowed.FirstOrDefault(x => string.Equals(x, val, StringComparison.OrdinalIgnoreCase));
        if (normalized == null)
            throw new ArgumentException($"无效的数据范围策略: {val}", nameof(input.DataScope));

        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null) return; // 或抛出 KeyNotFoundException

        // 更新角色上的策略字段（通过领域方法规范化）
        role.SetDataScope(normalized);
        await _roleRepository.UpdateAsync(role, cancellationToken);

        // 通过领域服务替换映射（含清理与新增，内部自带事务）
        await _roleDataScopeService.ReplaceAsync(role, normalized, input.DepartmentIds ?? Enumerable.Empty<long>(), cancellationToken);
    }
}
