using SqlSugar;
using Ginkgo.Domain.Utils;

namespace Ginkgo.Domain.Auth;

/// <summary>
/// Refresh Token 实体，支持令牌轮换（Rotation）。
/// </summary>
[SugarTable("ginkgo_Sys_RefreshToken", TableDescription = "刷新令牌表")]
public class RefreshToken : Entity
{
    /// <summary>
    /// 令牌值（随机字符串）。
    /// </summary>
    [SugarColumn(Length = 128, ColumnDescription = "刷新令牌值")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 关联用户 ID。
    /// </summary>
    [SugarColumn(ColumnDescription = "关联用户Id")]
    public long UserId { get; set; }

    /// <summary>
    /// 过期时间。
    /// </summary>
    [SugarColumn(ColumnDescription = "过期时间")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 是否已吊销。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否已吊销")]
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 吊销时间。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "吊销时间")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 创建时的客户端 IP。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "创建时客户端IP")]
    public string? CreatedByIp { get; set; }

    /// <summary>
    /// 轮换后替代此令牌的新令牌值（用于追踪令牌链）。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "替代令牌（轮换链）")]
    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 令牌是否有效（未过期且未吊销）。
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsActive => !IsRevoked && DateTime.Now < ExpiresAt;
}
