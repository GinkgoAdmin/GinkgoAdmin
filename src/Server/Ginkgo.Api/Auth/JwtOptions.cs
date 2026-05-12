// 文件功能说明：
// 定义 JWT 配置项（签名密钥、发行者、受众、过期时间）。

namespace Ginkgo.Api.Auth;

/// <summary>
/// JWT 配置项。
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// 签名密钥。
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// 发行者。
    /// </summary>
    public string Issuer { get; set; } = "ginkgo";

    /// <summary>
    /// 受众。
    /// </summary>
    public string Audience { get; set; } = "ginkgo-clients";

    /// <summary>
    /// 过期（分钟）。
    /// </summary>
    public int ExpiresMinutes { get; set; } = 120;

    /// <summary>
    /// Refresh Token 过期（分钟），默认 1440（24 小时）。
    /// 可在后台系统配置中调整。
    /// </summary>
    public int RefreshTokenExpiresMinutes { get; set; } = 1440;
}






