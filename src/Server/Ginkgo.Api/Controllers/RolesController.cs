// 文件功能说明：
// 角色模块 API 控制器，占位实现。

using Ginkgo.Application.Roles;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 角色接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/roles")]
[Authorize(Policy = "Permission")]
[ApiVersion("1.0")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleAppService _service;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="service">角色应用服务。</param>
    public RolesController(IRoleAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字。</param>
    [HttpGet]
    public async Task<Result<PagedResult<RoleListItemDto>>> GetAsync([FromQuery] PageRequest request, [FromQuery] string? keyword)
    {
        var data = await _service.GetPagedAsync(request, keyword);
        return Result<PagedResult<RoleListItemDto>>.Success(data);
    }

    /// <summary>
    /// 角色树（按编码层级约定构造）。
    /// </summary>
    [HttpGet("tree")]
    public async Task<Result<List<RoleTreeNodeDto>>> GetTreeAsync()
    {
        var data = await _service.GetRoleTreeAsync();
        return Result<List<RoleTreeNodeDto>>.Success(data);
    }

    /// <summary>
    /// 获取角色详情。
    /// </summary>
    /// <param name="id">角色 Id（Snowflake ID）。</param>
    [HttpGet("{id}")]
    public async Task<Result<RoleDetailDto>> GetByIdAsync(long id)
    {
        var dto = await _service.GetAsync(id);
        if (dto == null) return Result<RoleDetailDto>.Fail(404, "未找到角色");
        return Result<RoleDetailDto>.Success(dto);
    }

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="input">创建输入。</param>
    [HttpPost]
    public async Task<Result<long>> CreateAsync([FromBody] CreateRoleInput input)
    {
        var id = await _service.CreateAsync(input);
        return Result<long>.Success(id, "创建成功");
    }

    /// <summary>
    /// 更新。
    /// </summary>
    /// <param name="id">角色 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    [HttpPut("{id}")]
    public async Task<Result> UpdateAsync(long id, [FromBody] UpdateRoleInput input)
    {
        await _service.UpdateAsync(id, input);
        return Result.Success("更新成功");
    }

    /// <summary>
    /// 删除。
    /// </summary>
    /// <param name="id">角色 Id（Snowflake ID）。</param>
    [HttpDelete("{id}")]
    public async Task<Result> DeleteAsync(long id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return Result.Success("删除成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 获取全部权限（用于分配）。
    /// </summary>
    [HttpGet("permissions/all")]
    public async Task<Result<List<PermissionItemDto>>> GetAllPermissions()
    {
        var list = await _service.GetAllPermissionsAsync();
        return Result<List<PermissionItemDto>>.Success(list);
    }

    /// <summary>
    /// 获取某角色已分配的权限 Id。
    /// </summary>
    [HttpGet("{id}/permissions")]
    public async Task<Result<List<long>>> GetRolePermissionIds(long id)
    {
        var list = await _service.GetRolePermissionIdsAsync(id);
        return Result<List<long>>.Success(list);
    }

    /// <summary>
    /// 保存某角色的权限分配。
    /// </summary>
    [HttpPost("{id}/permissions")]
    public async Task<Result> SaveRolePermissions(long id, [FromBody] long[] permissionIds)
    {
        await _service.SaveRolePermissionsAsync(id, permissionIds);
        return Result.Success("保存成功");
    }

    /// <summary>
    /// 获取基于菜单的权限树（目录/菜单项/按钮）。
    /// </summary>
    [HttpGet("permissions/tree")]
    public async Task<Result<List<PermissionTreeNodeDto>>> GetPermissionTree()
    {
        var tree = await _service.GetPermissionTreeAsync();
        return Result<List<PermissionTreeNodeDto>>.Success(tree);
    }

    /// <summary>
    /// 获取角色数据范围设置（策略 + 指定部门列表）。
    /// </summary>
    [HttpGet("{id}/data-scope")]
    public async Task<Result<RoleDataScopeDto>> GetDataScope(long id)
    {
        var dto = await _service.GetDataScopeAsync(id);
        return Result<RoleDataScopeDto>.Success(dto);
    }

    /// <summary>
    /// 设置角色数据范围（当策略为 SpecifiedDepartments 时需要提供部门列表）。
    /// </summary>
    [HttpPost("{id}/data-scope")]
    public async Task<Result> SetDataScope(long id, [FromBody] SetRoleDataScopeInput input)
    {
        await _service.SetDataScopeAsync(id, input);
        return Result.Success("保存成功");
    }

}


