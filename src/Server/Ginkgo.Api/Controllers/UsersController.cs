// 文件功能说明：
// 用户模块 API 控制器，占位实现，返回规范结果。

using Ginkgo.Application.Users;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using Ginkgo.Domain;
using Ginkgo.ServerToolkit;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 用户接口。
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
[ApiVersion("1.0")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserAppService _service;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="service">用户应用服务。</param>
    public UsersController(IUserAppService service)
    {
        _service = service;
    }

    private static bool TryGetCurrentUserId(ClaimsPrincipal? principal, out long userId)
    {
        userId = default;
        if (principal == null) return false;
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)
                    ?? principal.FindFirst(c => string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase))
                    ?? principal.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase) || c.Type.EndsWith("/sub", StringComparison.OrdinalIgnoreCase));
        if (claim == null || string.IsNullOrWhiteSpace(claim.Value)) return false;
        return long.TryParse(claim.Value, out userId);
    }

    private async Task<bool> IsAdminAsync(long userId)
    {
        try
        {
            // 先从令牌的角色声明判断
            if (User?.IsInRole("ADMIN") == true) return true;
            if (userId == 0) return false;
            // 回退：查询用户是否绑定了固定的管理员角色 Id
            var userRoleRepo = HttpContext.RequestServices.GetRequiredService<IRepository<Ginkgo.Domain.Users.UserRole>>();
            var adminId = 1L; // Snowflake ID for admin role (typically the first role created)
            return await userRoleRepo.Query().AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminId);
        }
        catch { return false; }
    }

    /// <summary>
    /// 检查用户是否拥有"用户管理"菜单权限（通过角色→角色权限→菜单 Route 匹配）。
    /// 用于允许非 ADMIN 但有用户管理权限的角色读取其他用户的角色/部门信息。
    /// </summary>
    private async Task<bool> HasUserManagePermissionAsync(long userId)
    {
        try
        {
            var userRoleRepo = HttpContext.RequestServices.GetRequiredService<IRepository<Ginkgo.Domain.Users.UserRole>>();
            var rolePermRepo = HttpContext.RequestServices.GetRequiredService<IRepository<Ginkgo.Domain.Roles.RolePermission>>();
            var menuRepo = HttpContext.RequestServices.GetRequiredService<IRepository<Ginkgo.Domain.Menus.Menu>>();

            var roleIds = await userRoleRepo.Query()
                .Where(x => x.UserId == userId)
                .Select(x => x.RoleId)
                .Distinct()
                .ToListAsync();
            if (roleIds.Count == 0) return false;

            var grantedMenuIds = await rolePermRepo.Query()
                .Where(x => roleIds.Contains(x.RoleId))
                .Select(x => x.PermissionId)
                .Distinct()
                .ToListAsync();
            if (grantedMenuIds.Count == 0) return false;

            // 检查是否拥有"用户管理"菜单（Route = '/system/users'）的权限
            return await menuRepo.Query()
                .AnyAsync(m => grantedMenuIds.Contains(m.Id) && m.Route == "/system/users");
        }
        catch { return false; }
    }

    /// <summary>
    /// 分页查询（统一过滤器）。
    /// 前端将所有搜索条件打包在 query.filter(JSON) 中，并附带 sort（field:order）。
    /// 兼容旧参数 keyword。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="filter">JSON 字符串的筛选条件。</param>
    /// <param name="sort">排序，如 userName:ascending。</param>
    /// <param name="keyword">兼容旧的关键字参数。</param>
    [HttpGet]
    public async Task<Result<PagedResult<UserListItemDto>>> GetAsync(
        [FromQuery] PageRequest request,
        [FromQuery] string? filter,
        [FromQuery] string? sort,
        [FromQuery] string? keyword)
    {
        // 解析 filter
        var filters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(filter);
                if (dict != null)
                {
                    foreach (var kv in dict)
                        filters[kv.Key] = kv.Value;
                }
            }
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filters["keyword"] = keyword;
        }

        // 排序解析（此处仅透传给应用服务，或在此拆解）
        string? sortField = null;
        string? sortOrder = null;
        if (!string.IsNullOrWhiteSpace(sort))
        {
            var parts = sort.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2) { sortField = parts[0]; sortOrder = parts[1]; }
        }

        // 优先使用统一过滤接口
        var data = await _service.SearchPagedAsync(request, filters, sortField, sortOrder);
        return Result<PagedResult<UserListItemDto>>.Success(data);
    }

    /// <summary>
    /// 获取“本人”详情。
    /// </summary>
    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<Result<UserDetailDto>> GetMeAsync()
    {
        // 未登录返回 401（使用手动检查，而非 Permission 策略）
        if (!TryGetCurrentUserId(User, out var userId)) return Result<UserDetailDto>.Fail(401, "未登录");
        var data = await _service.GetAsync(userId);
        if (data == null) return Result<UserDetailDto>.Fail(404, "用户不存在");
        return Result<UserDetailDto>.Success(data);
    }

    /// <summary>
    /// 更新“本人”资料。
    /// </summary>
    public sealed class UpdateMeInput
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string? Introduction { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        /// <summary>修改邮箱时的验证码（注册模式要求邮箱验证时必填）</summary>
        public string? EmailCode { get; set; }
        /// <summary>修改手机时的验证码（注册模式要求手机验证时必填）</summary>
        public string? PhoneCode { get; set; }
    }
    [HttpPut("me")]
    [AllowAnonymous]
    public async Task<Result> UpdateMeAsync(
        [FromBody] UpdateMeInput input,
        [FromServices] SqlSugar.ISqlSugarClient db,
        [FromServices] ISecondaryVerificationService verificationService,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(User, out var userId)) return Result.Fail(401, "未登录");
        var me = await _service.GetAsync(userId);
        if (me == null) return Result.Fail(404, "用户不存在");

        // 读取注册模式配置
        var modeSetting = await db.Ado.GetStringAsync(
            "SELECT `Value` FROM ginkgo_Sys_Settings WHERE `Key` = 'Registration.Mode' LIMIT 1");
        var mode = string.IsNullOrWhiteSpace(modeSetting) ? "free" : modeSetting.Trim().ToLowerInvariant();

        var needEmailVerify = mode is "email_code" or "both_code";
        var needPhoneVerify = mode is "phone_code" or "both_code";

        // 检测邮箱是否发生变更
        var emailChanged = !string.IsNullOrWhiteSpace(input.Email)
            && !string.Equals(input.Email?.Trim(), me.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
        // 检测手机号是否发生变更
        var phoneChanged = !string.IsNullOrWhiteSpace(input.Phone)
            && !string.Equals(input.Phone?.Trim(), me.Phone?.Trim(), StringComparison.Ordinal);

        // 邮箱变更需验证码
        if (needEmailVerify && emailChanged)
        {
            if (string.IsNullOrWhiteSpace(input.EmailCode))
                return Result.Fail(4001, "修改邮箱需要验证码，请先获取邮箱验证码");
            var result = await verificationService.ValidateVerificationCodeAsync(
                target: input.Email!.Trim(),
                purpose: VerificationPurpose.BindEmail,
                code: input.EmailCode.Trim(),
                consumeOnSuccess: true, ct: ct);
            if (!result.Success)
                return Result.Fail(4001, result.Message ?? "邮箱验证码无效或已过期");
        }

        // 手机变更需验证码
        if (needPhoneVerify && phoneChanged)
        {
            if (string.IsNullOrWhiteSpace(input.PhoneCode))
                return Result.Fail(4001, "修改手机号需要验证码，请先获取手机验证码");
            var result = await verificationService.ValidateVerificationCodeAsync(
                target: input.Phone!.Trim(),
                purpose: VerificationPurpose.BindPhone,
                code: input.PhoneCode.Trim(),
                consumeOnSuccess: true, ct: ct);
            if (!result.Success)
                return Result.Fail(4001, result.Message ?? "手机验证码无效或已过期");
        }

        await _service.UpdateAsync(userId, new UpdateUserInput
        {
            DisplayName = input.DisplayName,
            Avatar = input.Avatar,
            Introduction = input.Introduction,
            Email = input.Email,
            Phone = input.Phone,
            Enabled = me.Enabled
        });
        return Result.Success("更新成功");
    }

    /// <summary>
    /// 修改“本人”密码。
    /// </summary>
    [HttpPost("me/password")]
    [AllowAnonymous]
    public async Task<Result> ChangeMyPassword([FromBody] ChangePasswordInput input)
    {
        if (!TryGetCurrentUserId(User, out var userId)) return Result.Fail(401, "未登录");
        await _service.ChangePasswordAsync(userId, input);
        return Result.Success("密码修改成功");
    }



    /// <summary>
    /// 获取明细。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    [HttpGet("{id}")]
    public async Task<Result<UserDetailDto>> GetByIdAsync(long id)
    {
        var data = await _service.GetAsync(id);
        if (data == null) return Result<UserDetailDto>.Fail(404, "用户不存在");
        return Result<UserDetailDto>.Success(data);
    }

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="input">创建输入。</param>
    [HttpPost]
    public async Task<Result<long>> CreateAsync([FromBody] CreateUserInput input)
    {
        var id = await _service.CreateAsync(input);
        return Result<long>.Success(id, "创建成功");
    }

    /// <summary>
    /// 更新。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    [HttpPut("{id}")]
    public async Task<Result> UpdateAsync(long id, [FromBody] UpdateUserInput input)
    {
        await _service.UpdateAsync(id, input);
        return Result.Success("更新成功");
    }

    /// <summary>
    /// 删除。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    [HttpDelete("{id}")]
    public async Task<Result> DeleteAsync(long id)
    {
        await _service.DeleteAsync(id);
        return Result.Success("删除成功");
    }

    /// <summary>
    /// 获取用户的角色 Id 列表。
    /// </summary>
    [HttpGet("{id}/roles")]
    [AllowAnonymous] // 自行进行登录与管理员/本人判断
    public async Task<Result<List<long>>> GetUserRoleIds(long id)
    {
        if (!TryGetCurrentUserId(User, out var currentUserId))
            return Result<List<long>>.Fail(401, "未登录");

        // 本人可以读取；管理员可以读取任意用户；拥有用户管理权限的角色也可以读取
        if (currentUserId != id && !await IsAdminAsync(currentUserId) && !await HasUserManagePermissionAsync(currentUserId))
            return Result<List<long>>.Fail(403, "仅可读取本人或需要管理员权限");

        var list = await _service.GetUserRoleIdsAsync(id);
        return Result<List<long>>.Success(list);
    }

    /// <summary>
    /// 保存用户的角色 Id 列表。
    /// </summary>
    [HttpPost("{id}/roles")]
    public async Task<Result> SaveUserRoles(long id, [FromBody] long[] roleIds)
    {
        if (!TryGetCurrentUserId(User, out var currentUserId))
            return Result.Fail(401, "未登录");
        if (!await IsAdminAsync(currentUserId) && !await HasUserManagePermissionAsync(currentUserId))
            return Result.Fail(403, "需要用户管理权限");

        await _service.SaveUserRolesAsync(id, roleIds);
        return Result.Success("保存成功");
    }

    /// <summary>
    /// 获取用户的部门 Id 列表。
    /// </summary>
    [HttpGet("{id}/departments")]
    [AllowAnonymous]
    public async Task<Result<List<long>>> GetUserDepartmentIds(long id)
    {
        if (!TryGetCurrentUserId(User, out var currentUserId))
            return Result<List<long>>.Fail(401, "未登录");

        if (currentUserId != id && !await IsAdminAsync(currentUserId) && !await HasUserManagePermissionAsync(currentUserId))
            return Result<List<long>>.Fail(403, "仅可读取本人或需要管理员权限");

        var list = await _service.GetUserDepartmentIdsAsync(id);
        return Result<List<long>>.Success(list);
    }

    /// <summary>
    /// 保存用户的部门 Id 列表。
    /// </summary>
    [HttpPost("{id}/departments")]
    public async Task<Result> SaveUserDepartments(long id, [FromBody] long[] departmentIds)
    {
        if (!TryGetCurrentUserId(User, out var currentUserId))
            return Result.Fail(401, "未登录");
        if (!await IsAdminAsync(currentUserId) && !await HasUserManagePermissionAsync(currentUserId))
            return Result.Fail(403, "需要用户管理权限");

        await _service.SaveUserDepartmentsAsync(id, departmentIds);
        return Result.Success("保存成功");
    }


    /// <summary>
    /// 管理员修改指定用户密码。
    /// </summary>
    [HttpPost("{id}/password")]
    public async Task<Result> ChangePassword(long id, [FromBody] ChangePasswordInput input)
    {
        await _service.ChangePasswordAsync(id, input);
        return Result.Success("密码修改成功");
    }

    /// <summary>
    /// 管理员重置指定用户密码。
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<Result> ResetPassword(long id, [FromBody] ResetPasswordInput input)
    {
        if (!TryGetCurrentUserId(User, out var currentUserId))
            return Result.Fail(401, "未登录");
        if (!await IsAdminAsync(currentUserId) && !await HasUserManagePermissionAsync(currentUserId))
            return Result.Fail(403, "需要用户管理权限");

        await _service.ChangePasswordAsync(id, new ChangePasswordInput { OldPassword = string.Empty, NewPassword = input.NewPassword }, default, skipOldPasswordCheck: true);
        return Result.Success("重置密码成功");
    }

}



