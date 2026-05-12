using SqlSugar;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Domain.Users;

/// <summary>
/// 找回密码令牌（仅存储哈希，避免明文泄露）。
/// </summary>
[SugarTable("ginkgo_Sys_PasswordResetToken")] // 统一命名
public sealed class PasswordResetToken
{
    [SugarColumn(IsPrimaryKey = true)]
    public long Id { get; set; } = SnowflakeIdGenerator.NextId();

    /// <summary>User Id。</summary>
    public long UserId { get; set; }

    /// <summary>
    /// 令牌哈希（SHA256）。
    /// </summary>
    [SugarColumn(Length = 128)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>过期时间（UTC）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>使用时间（UTC）。</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

