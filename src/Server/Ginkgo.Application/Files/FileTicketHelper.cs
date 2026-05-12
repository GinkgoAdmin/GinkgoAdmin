using System.Security.Cryptography;
using System.Text;

namespace Ginkgo.Application.Files;

/// <summary>
/// 文件短期访问票据工具（HMAC 签名 URL，无状态）。
/// 用于已登录用户获取文件的短期匿名访问链接（5-10 分钟有效）。
/// 签名方式：HMACSHA256("{fileId}:{expUnixSeconds}", signingKey) → Base64Url。
/// </summary>
public sealed class FileTicketHelper
{
    private readonly byte[] _keyBytes;

    /// <summary>
    /// 构造函数，接收 HMAC 签名密钥（复用 JWT SigningKey）。
    /// </summary>
    /// <param name="signingKey">签名密钥字符串</param>
    public FileTicketHelper(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("签名密钥不能为空", nameof(signingKey));
        _keyBytes = Encoding.UTF8.GetBytes(signingKey);
    }

    /// <summary>
    /// 生成文件短期访问票据 URL。
    /// </summary>
    /// <param name="fileId">文件Id</param>
    /// <param name="expiresMinutes">有效期（分钟），默认 10</param>
    /// <returns>签名 URL，格式如 /api/v1/files/ticket?id={fileId}&amp;exp={unix}&amp;sig={hmac}</returns>
    public string GenerateTicketUrl(long fileId, int expiresMinutes = 10)
    {
        if (fileId <= 0) throw new ArgumentException("文件Id无效", nameof(fileId));
        if (expiresMinutes <= 0) expiresMinutes = 10;

        var expUnix = DateTimeOffset.Now.AddMinutes(expiresMinutes).ToUnixTimeSeconds();
        var sig = ComputeSignature(fileId, expUnix);

        return $"/api/v1/files/ticket?id={fileId}&exp={expUnix}&sig={Uri.EscapeDataString(sig)}";
    }

    /// <summary>
    /// 验证票据签名和有效期。
    /// </summary>
    /// <param name="fileId">文件Id</param>
    /// <param name="expUnix">过期时间戳（Unix 秒）</param>
    /// <param name="sig">签名值</param>
    /// <returns>true=有效，false=签名不匹配或已过期</returns>
    public bool ValidateTicket(long fileId, long expUnix, string sig)
    {
        if (fileId <= 0 || string.IsNullOrWhiteSpace(sig)) return false;

        // 检查过期
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (expUnix < now) return false;

        // 检查签名
        var expected = ComputeSignature(fileId, expUnix);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(sig));
    }

    /// <summary>
    /// 计算 HMAC-SHA256 签名。
    /// </summary>
    private string ComputeSignature(long fileId, long expUnix)
    {
        var payload = $"{fileId}:{expUnix}";
        using var hmac = new HMACSHA256(_keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Base64Url 编码（去 padding，替换 +/ 为 -_）。
    /// </summary>
    private static string Base64UrlEncode(byte[] data)
    {
        var s = Convert.ToBase64String(data);
        s = s.Split('=')[0];
        s = s.Replace('+', '-');
        s = s.Replace('/', '_');
        return s;
    }
}
