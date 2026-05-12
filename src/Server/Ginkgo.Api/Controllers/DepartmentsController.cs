using Ginkgo.Application.Departments;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/departments")]
[Authorize]
[ApiVersion("1.0")]
    public sealed class DepartmentsController : ControllerBase
{
    private readonly IDepartmentAppService _service;
    public DepartmentsController(IDepartmentAppService service) { _service = service; }

    [HttpGet]
    public async Task<Result<PagedResult<DepartmentListItemDto>>> GetAsync([FromQuery] PageRequest request, [FromQuery] string? keyword)
    {
        var data = await _service.GetPagedAsync(request, keyword);
        return Result<PagedResult<DepartmentListItemDto>>.Success(data);
    }

    [HttpGet("tree/all")]
    public async Task<Result<List<DepartmentTreeNodeDto>>> GetAllTreeAsync()
    {
        var data = await _service.GetAllTreeAsync();
        return Result<List<DepartmentTreeNodeDto>>.Success(data);
    }

    [HttpGet("{id}")]
    public async Task<Result<DepartmentDetailDto?>> GetByIdAsync(long id)
    {
        var data = await _service.GetAsync(id);
        if (data == null) return Result<DepartmentDetailDto?>.Fail(404, "部门不存在");
        return Result<DepartmentDetailDto?>.Success(data);
    }

    /// <summary>
    /// 获取某部门下的用户（显示名、手机号、邮箱）。
    /// </summary>
    [HttpGet("{id}/users")]
    public async Task<Result<List<Ginkgo.Application.Departments.DepartmentUserDto>>> GetUsersByDepartment(long id)
    {
        var list = await _service.GetUsersByDepartmentAsync(id);
        return Result<List<Ginkgo.Application.Departments.DepartmentUserDto>>.Success(list);
    }

    [HttpDelete("{id}/users/{userId}")]
    public async Task<Result> RemoveUser(long id, long userId)
    {
        await _service.RemoveUserAsync(id, userId);
        return Result.Success("已移除");
    }

    public sealed class SetManagerInput { public bool IsManager { get; set; } }
    [HttpPost("{id}/users/{userId}/manager")]
    public async Task<Result> SetManager(long id, long userId, [FromBody] SetManagerInput input)
    {
        await _service.SetManagerAsync(id, userId, input.IsManager);
        return Result.Success("已更新");
    }

    [HttpPost]
    public async Task<Result<long>> CreateAsync([FromBody] CreateDepartmentInput input)
    {
        var id = await _service.CreateAsync(input);
        return Result<long>.Success(id, "创建成功");
    }

    [HttpPut("{id}")]
    public async Task<Result> UpdateAsync(long id, [FromBody] UpdateDepartmentInput input)
    {
        await _service.UpdateAsync(id, input);
        return Result.Success("更新成功");
    }

    [HttpDelete("{id}")]
    public async Task<Result> DeleteAsync(long id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return Result.Success("删除成功");
        }
        catch (InvalidOperationException ex)
        {
            // 当存在子部门或仍有关联用户时，业务层会抛出 InvalidOperationException
            return Result.Fail(400, ex.Message);
        }
        catch (Exception ex)
        {
            // 兜底异常，避免前端出现“删除成功但数据未变化”的错觉
            return Result.Fail(500, ex.Message);
        }
    }
}




