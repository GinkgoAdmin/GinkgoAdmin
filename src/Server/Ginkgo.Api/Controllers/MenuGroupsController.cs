// 文件功能说明：
// 菜单组管理 API 控制器，提供菜单组 CRUD、菜单项管理、导航查询和角色授权接口。

using Ginkgo.Application.Menus;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 菜单组管理接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/menu-groups")]
[Authorize(Policy = "Permission")]
[ApiVersion("1.0")]
public sealed class MenuGroupsController : ControllerBase
{
    private readonly IMenuGroupAppService _service;

    public MenuGroupsController(IMenuGroupAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 从 JWT Claims 中提取当前用户 Id。
    /// </summary>
    private long? GetCurrentUserId()
    {
        var uid = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var userId)) return userId;
        return null;
    }

    // ===== 菜单组管理 =====

    /// <summary>
    /// 获取菜单组列表。
    /// </summary>
    [HttpGet]
    public async Task<Result<List<MenuGroupListItemDto>>> GetListAsync()
    {
        var data = await _service.GetGroupListAsync();
        return Result<List<MenuGroupListItemDto>>.Success(data);
    }

    /// <summary>
    /// 获取菜单组详情。
    /// </summary>
    [HttpGet("{id}")]
    public async Task<Result<MenuGroupDetailDto>> GetAsync(long id)
    {
        var data = await _service.GetGroupAsync(id);
        if (data == null) return Result<MenuGroupDetailDto>.Fail(404, "菜单组不存在");
        return Result<MenuGroupDetailDto>.Success(data);
    }

    /// <summary>
    /// 创建菜单组。
    /// </summary>
    [HttpPost]
    public async Task<Result<long>> CreateAsync([FromBody] CreateMenuGroupInput input)
    {
        try
        {
            var id = await _service.CreateGroupAsync(input);
            return Result<long>.Success(id, "创建成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result<long>.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 更新菜单组。
    /// </summary>
    [HttpPut("{id}")]
    public async Task<Result> UpdateAsync(long id, [FromBody] UpdateMenuGroupInput input)
    {
        try
        {
            await _service.UpdateGroupAsync(id, input);
            return Result.Success("更新成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 删除菜单组。
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<Result> DeleteAsync(long id)
    {
        try
        {
            await _service.DeleteGroupAsync(id);
            return Result.Success("删除成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result.Fail(400, ioe.Message);
        }
    }

    // ===== 菜单组项管理 =====

    /// <summary>
    /// 获取菜单组下的菜单项树。
    /// </summary>
    [HttpGet("{groupId}/items")]
    public async Task<Result<List<MenuGroupItemDto>>> GetItemsAsync(long groupId)
    {
        var data = await _service.GetItemTreeAsync(groupId);
        return Result<List<MenuGroupItemDto>>.Success(data);
    }

    /// <summary>
    /// 获取菜单组项详情。
    /// </summary>
    [HttpGet("{groupId}/items/{id}")]
    public async Task<Result<MenuGroupItemDto>> GetItemAsync(long groupId, long id)
    {
        var data = await _service.GetItemAsync(groupId, id);
        if (data == null) return Result<MenuGroupItemDto>.Fail(404, "菜单项不存在");
        return Result<MenuGroupItemDto>.Success(data);
    }

    /// <summary>
    /// 创建菜单组项。
    /// </summary>
    [HttpPost("{groupId}/items")]
    public async Task<Result<long>> CreateItemAsync(long groupId, [FromBody] CreateMenuGroupItemInput input)
    {
        try
        {
            var id = await _service.CreateItemAsync(groupId, input);
            return Result<long>.Success(id, "创建成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result<long>.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 更新菜单组项。
    /// </summary>
    [HttpPut("{groupId}/items/{id}")]
    public async Task<Result> UpdateItemAsync(long groupId, long id, [FromBody] UpdateMenuGroupItemInput input)
    {
        try
        {
            await _service.UpdateItemAsync(groupId, id, input);
            return Result.Success("更新成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result.Fail(400, ioe.Message);
        }
    }

    /// <summary>
    /// 删除菜单组项。
    /// </summary>
    [HttpDelete("{groupId}/items/{id}")]
    public async Task<Result> DeleteItemAsync(long groupId, long id)
    {
        await _service.DeleteItemAsync(groupId, id);
        return Result.Success("删除成功");
    }

    /// <summary>
    /// 批量删除菜单组项。
    /// </summary>
    [HttpPost("{groupId}/items/batch-delete")]
    public async Task<Result> BatchDeleteItemsAsync(long groupId, [FromBody] long[] ids)
    {
        await _service.BatchDeleteItemsAsync(groupId, ids);
        return Result.Success("批量删除成功");
    }

    /// <summary>
    /// 批量更新排序（拖拽排序）。
    /// </summary>
    [HttpPut("{groupId}/items/sort")]
    public async Task<Result> SortItemsAsync(long groupId, [FromBody] List<MenuGroupItemSortInput> items)
    {
        await _service.SortItemsAsync(groupId, items);
        return Result.Success("排序成功");
    }

    /// <summary>
    /// 从系统菜单导入到菜单组。
    /// </summary>
    [HttpPost("{groupId}/items/import-from-system")]
    public async Task<Result<List<long>>> ImportFromSystemAsync(long groupId, [FromBody] ImportFromSystemMenuInput input)
    {
        try
        {
            var ids = await _service.ImportFromSystemMenuAsync(groupId, input.MenuIds, input.ParentId);
            return Result<List<long>>.Success(ids, "导入成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result<List<long>>.Fail(400, ioe.Message);
        }
    }

    // ===== 角色菜单组权限 =====

    /// <summary>
    /// 获取角色已授权的菜单组 Id 列表。
    /// </summary>
    [HttpGet("role-permissions/{roleId}")]
    public async Task<Result<List<long>>> GetRoleMenuGroupsAsync(long roleId)
    {
        var data = await _service.GetRoleMenuGroupIdsAsync(roleId);
        return Result<List<long>>.Success(data);
    }

    /// <summary>
    /// 设置角色的菜单组权限。
    /// </summary>
    [HttpPut("role-permissions")]
    public async Task<Result> SetRoleMenuGroupsAsync([FromBody] SetRoleMenuGroupsInput input)
    {
        await _service.SetRoleMenuGroupsAsync(input);
        return Result.Success("设置成功");
    }

    // ===== 角色菜单组项（item 级）授权 =====

    /// <summary>
    /// 获取各端默认菜单组下的可授权入口项（供角色编辑器按端分组勾选）。
    /// </summary>
    [HttpGet("grantable-items")]
    public async Task<Result<List<GrantableMenuItemDto>>> GetGrantableItemsAsync()
    {
        var data = await _service.GetGrantableItemsAsync();
        return Result<List<GrantableMenuItemDto>>.Success(data);
    }

    /// <summary>
    /// 获取角色已授权的菜单组项 Id 列表。
    /// </summary>
    [HttpGet("role-item-permissions/{roleId}")]
    public async Task<Result<List<long>>> GetRoleMenuGroupItemsAsync(long roleId)
    {
        var data = await _service.GetRoleMenuGroupItemIdsAsync(roleId);
        return Result<List<long>>.Success(data);
    }

    /// <summary>
    /// 设置角色的菜单组项（item 级）授权（以提交集合全量覆盖）。
    /// </summary>
    [HttpPut("role-item-permissions")]
    public async Task<Result> SetRoleMenuGroupItemsAsync([FromBody] SetRoleMenuGroupItemsInput input)
    {
        await _service.SetRoleMenuGroupItemsAsync(input);
        return Result.Success("设置成功");
    }

    /// <summary>
    /// 将菜单组设为默认（每端唯一）。当 <c>ClientType</c> 含多个终端类型时返回 400 错误信息。
    /// </summary>
    [HttpPut("{id}/set-default")]
    public async Task<Result> SetGroupDefaultAsync(long id)
    {
        try
        {
            await _service.SetGroupDefaultAsync(id);
            return Result.Success("设置成功");
        }
        catch (InvalidOperationException ioe)
        {
            return Result.Fail(400, ioe.Message);
        }
    }
}

/// <summary>
/// 导航菜单公开查询接口（匿名可访问）。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/navigation")]
[ApiVersion("1.0")]
public sealed class NavigationController : ControllerBase
{
    private readonly IMenuGroupAppService _service;

    public NavigationController(IMenuGroupAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 从 JWT Claims 中提取当前用户 Id（可能为空）。
    /// </summary>
    private long? GetCurrentUserId()
    {
        var uid = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var userId)) return userId;
        return null;
    }

    /// <summary>
    /// 按 Slug 获取导航菜单（含权限过滤）。匿名可访问，已登录用户自动按权限过滤。
    /// </summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<Result<NavigationMenuDto>> GetNavigationAsync(string slug)
    {
        var userId = GetCurrentUserId();
        var data = await _service.GetNavigationAsync(slug, userId);
        if (data == null) return Result<NavigationMenuDto>.Fail(404, "导航菜单不存在或无权访问");
        return Result<NavigationMenuDto>.Success(data);
    }
}

/// <summary>
/// 从系统菜单导入输入。
/// </summary>
public sealed class ImportFromSystemMenuInput
{
    public long[] MenuIds { get; set; } = Array.Empty<long>();
    public long? ParentId { get; set; }
}
