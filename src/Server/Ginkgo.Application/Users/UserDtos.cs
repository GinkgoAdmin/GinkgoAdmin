// 文件功能说明：
// 定义用户模块的 DTO。

using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Users;

/// <summary>
/// 用户列表项输出。
/// </summary>
public sealed class UserListItemDto
{
    /// <summary>
    /// 主键（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户名。
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 手机号。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 部门名称集合（列表展示用）。
    /// </summary>
    public List<string> DepartmentNames { get; set; } = new();

    /// <summary>
    /// 角色名称集合（列表展示用）。
    /// </summary>
    public List<string> RoleNames { get; set; } = new();

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// 用户详情输出。
/// </summary>
public sealed class UserDetailDto
{
    /// <summary>
    /// 主键（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户名。
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 头像（文件路径或URL）。
    /// </summary>
    [MaxLength(500)]
    public string? Avatar { get; set; }

    /// <summary>
    /// 个人介绍。
    /// </summary>
    [MaxLength(1000)]
    public string? Introduction { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// 邮箱。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 手机号。
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 部门名称集合。
    /// </summary>
    public List<string> DepartmentNames { get; set; } = new();

    /// <summary>
    /// 角色名称集合。
    /// </summary>
    public List<string> RoleNames { get; set; } = new();
}

/// <summary>
/// 用户创建输入。
/// </summary>
public sealed class CreateUserInput
{
    /// <summary>
    /// 用户名。
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名。
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 密码。
    /// </summary>
    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱。
    /// </summary>
    [MaxLength(256)]
    public string? Email { get; set; }

    /// <summary>
    /// 手机号。
    /// </summary>
    [MaxLength(32)]
    public string? Phone { get; set; }
}

/// <summary>
/// 用户更新输入。
/// </summary>
public sealed class UpdateUserInput
{
    /// <summary>
    /// 显示名。
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 头像（文件路径或URL）。
    /// </summary>
    [MaxLength(500)]
    public string? Avatar { get; set; }

    /// <summary>
    /// 个人介绍。
    /// </summary>
    [MaxLength(1000)]
    public string? Introduction { get; set; }

    /// <summary>
    /// 邮箱。
    /// </summary>
    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; set; }

    /// <summary>
    /// 手机号。
    /// </summary>
    [MaxLength(32)]
    public string? Phone { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// 修改密码输入。
/// </summary>
public sealed class ChangePasswordInput
{
    [Required]
    [MaxLength(128)]
    public string OldPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 管理员重置密码输入。
/// </summary>
public sealed class ResetPasswordInput
{
    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}





/// <summary>
/// 用户注册输入。
/// </summary>
public sealed class RegisterInput
{
    /// <summary>用户名（邮箱/手机注册模式下可为空，后端自动用邮箱或手机号填充）。</summary>
    [MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>显示名（可为空，后端自动用 UserName 填充）。</summary>
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? Phone { get; set; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "两次密码输入不一致")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>邮箱验证码（邮箱+验证码模式时必填）。</summary>
    [MaxLength(16)]
    public string? EmailCode { get; set; }

    /// <summary>手机验证码（手机+验证码模式时必填）。</summary>
    [MaxLength(16)]
    public string? PhoneCode { get; set; }
}

/// <summary>
/// 检查账户联系方式输出。
/// </summary>
public sealed class CheckAccountContactOutput
{
    /// <summary>是否找到该账户。</summary>
    public bool Found { get; set; }
    /// <summary>是否有绑定邮箱。</summary>
    public bool HasEmail { get; set; }
    /// <summary>是否有绑定手机。</summary>
    public bool HasPhone { get; set; }
    /// <summary>脱敏后的邮箱（如 e***@g***.com）。</summary>
    public string? MaskedEmail { get; set; }
    /// <summary>脱敏后的手机号（如 138****5678）。</summary>
    public string? MaskedPhone { get; set; }
}

/// <summary>
/// 发起找回密码输入。
/// </summary>
public sealed class ForgotPasswordStartInput
{
    /// <summary>
    /// 邮箱（优先）或用户名。
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Account { get; set; } = string.Empty;

    /// <summary>
    /// 发送渠道：email / phone。默认 email。
    /// </summary>
    [MaxLength(16)]
    public string Channel { get; set; } = "email";
}

/// <summary>
/// 完成找回密码输入（使用6位验证码）。
/// </summary>
public sealed class ForgotPasswordResetInput
{
    /// <summary>
    /// 发起找回密码时使用的账号（邮箱或用户名）。
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Account { get; set; } = string.Empty;

    /// <summary>
    /// 用户输入的6位验证码。
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
