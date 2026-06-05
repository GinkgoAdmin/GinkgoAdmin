// 文件功能说明：
// 定义用户应用服务接口。

using Ginkgo.Shared;

namespace Ginkgo.Application.Users;

/// <summary>
/// 用户应用服务接口。
/// </summary>
public interface IUserAppService
{
    /// <summary>
    /// 分页查询用户。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="keyword">关键字（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PagedResult<UserListItemDto>> GetPagedAsync(PageRequest request, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询（统一过滤器 + 关联条件）。
    /// </summary>
    /// <param name="request">分页参数。</param>
    /// <param name="filters">筛选条件（包含 relations）。</param>
    /// <param name="sortField">排序字段。</param>
    /// <param name="sortOrder">排序方向（ascending/descending）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PagedResult<UserListItemDto>> SearchPagedAsync(PageRequest request, IDictionary<string, object?> filters, string? sortField, string? sortOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户详情。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<UserDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建用户。
    /// </summary>
    /// <param name="input">创建输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<long> CreateAsync(CreateUserInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 前台注册（公开入口）。
    /// </summary>
    Task<long> RegisterAsync(RegisterInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查账户的联系方式（邮箱/手机），用于找回密码前判断可用渠道。
    /// </summary>
    Task<CheckAccountContactOutput> CheckAccountContactAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发起找回密码（发送6位验证码）。
    /// </summary>
    Task ForgotPasswordStartAsync(ForgotPasswordStartInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成找回密码（持验证码设置新密码）。
    /// </summary>
    Task ForgotPasswordResetAsync(ForgotPasswordResetInput input, CancellationToken cancellationToken = default);


    /// <summary>
    /// 更新用户。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    /// <param name="input">更新输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateAsync(long id, UpdateUserInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用户自助注销帐号（需验证密码）。
    /// </summary>
    Task DeleteSelfAsync(long id, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除用户可选个人信息（邮箱、手机、头像、简介）。
    /// </summary>
    Task ClearPersonalInfoAsync(long id, CancellationToken cancellationToken = default);

    // 关联：角色
    Task<List<long>> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken = default);
    Task SaveUserRolesAsync(long userId, IEnumerable<long> roleIds, CancellationToken cancellationToken = default);

    // 关联：部门
    Task<List<long>> GetUserDepartmentIdsAsync(long userId, CancellationToken cancellationToken = default);
    Task SaveUserDepartmentsAsync(long userId, IEnumerable<long> departmentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 修改密码。
    /// </summary>
    /// <param name="id">用户 Id（Snowflake ID）。</param>
    /// <param name="input">修改密码输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="skipOldPasswordCheck">是否跳过旧密码校验（管理员重置时使用）。</param>
    Task ChangePasswordAsync(long id, ChangePasswordInput input, CancellationToken cancellationToken = default, bool skipOldPasswordCheck = false);
}






