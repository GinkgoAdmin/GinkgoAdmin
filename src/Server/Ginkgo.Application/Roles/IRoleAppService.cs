// 文件功能说明：
// 定义角色应用服务接口。

using Ginkgo.Shared;

namespace Ginkgo.Application.Roles;

/// <summary>
/// 角色应用服务接口。
/// </summary>
public interface IRoleAppService
{
    /// <summary>
    /// 分页查询角色。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PagedResult<RoleListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建角色。
    /// </summary>
    /// <param name="input">创建输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<long> CreateAsync(CreateRoleInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新角色。
    /// </summary>
    /// <param name="id">角色 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateAsync(long id, UpdateRoleInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除角色。
    /// </summary>
    /// <param name="id">角色 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取全部权限（用于分配）。
    /// </summary>
    Task<List<PermissionItemDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取某角色的权限 Id 列表。
    /// </summary>
    Task<List<long>> GetRolePermissionIdsAsync(long roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存角色权限。
    /// </summary>
    Task SaveRolePermissionsAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取基于菜单的权限树（目录/菜单项/按钮）。
    /// 叶子节点（或带权限码的节点）会携带 PermissionId 以供分配。
    /// </summary>
    Task<List<PermissionTreeNodeDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取角色树（支持上/下级）。
    /// 简化实现：以 Code 或 Name 的层级约定构造树；若数据库后续新增 ParentId，可改为真实父子关系。
    /// </summary>
    Task<List<RoleTreeNodeDto>> GetRoleTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 Id 获取角色详情（用于编辑对话框回显）。
    /// </summary>
    Task<RoleDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取角色数据范围设置（策略 + 指定部门列表）。
        /// </summary>
        Task<RoleDataScopeDto> GetDataScopeAsync(long roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置角色数据范围（当策略为 SpecifiedDepartments 时需要提供部门列表）。
        /// </summary>
        Task SetDataScopeAsync(long roleId, SetRoleDataScopeInput input, CancellationToken cancellationToken = default);

}






