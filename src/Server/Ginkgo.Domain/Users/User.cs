// 文件功能说明：
// 定义系统用户领域实体，包含基础属性与审计字段。

using SqlSugar;

namespace Ginkgo.Domain.Users;

/// <summary>
/// 用户实体。
/// </summary>
[SugarTable("ginkgo_Sys_User")]
[SugarIndex("IX_User_UserName", nameof(UserName), OrderByType.Asc, true)] // 唯一索引

public sealed class User : AuditableEntity
{
    /// <summary>
    /// 用户名（唯一）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 密码盐（用于加盐哈希）。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Salt { get; set; }

    /// <summary>
    /// 邮箱。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Email { get; set; }

    /// <summary>
    /// 手机号。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Phone { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最后登录时间（UTC）。
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 头像（文件路径或URL）。
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "头像（文件路径或URL）")]
    public string? Avatar { get; set; }

    /// <summary>
    /// 个人介绍。
    /// </summary>
    [SugarColumn(Length = 1000, IsNullable = true, ColumnDescription = "个人介绍")]
    public string? Introduction { get; set; }

    /// <summary>
    /// 主属部门（用于数据范围的部门过滤；多部门场景仍通过 UserDepartment 表维护）。
    /// 注意：当前数据库尚无该列，为避免运行时查询错误，暂不映射到物理列；
    /// 待数据库添加列后，去掉 IsIgnore=true 即可。
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public long? DepartmentId { get; set; }

    /// <summary>
    /// 设置密码（领域逻辑）：使用领域密码哈希服务生成哈希与盐。
    /// </summary>
    public void SetPassword(string rawPassword, Ginkgo.Domain.Users.IPasswordHasher hasher)
    {
        var hash = hasher.Hash(rawPassword, out var salt);
        PasswordHash = hash;
        Salt = salt;
    }
}
