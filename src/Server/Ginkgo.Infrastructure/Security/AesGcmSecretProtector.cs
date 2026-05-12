// 文件功能说明：
// IConnectionSecretProtector 默认实现，AES-256-GCM。密钥来源：
//   1) 环境变量 GINKGO_TENANT_DB_KEY（推荐生产环境使用）
//   2) appsettings.json 的 Database:TenantDbKey
//   3) 启动期随机生成（仅开发态兜底；重启后历史密文将无法解密，启动日志会 WARN）
// 字符串密钥长度任意，经 SHA-256 摘要后取 32 字节作为 AES-256 主密钥，避免对运维人员强制 base64 字符串。

using System.Security.Cryptography;
using System.Text;
using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Security;

/// <summary>
/// AES-256-GCM 实现的 <see cref="IConnectionSecretProtector"/>。
/// </summary>
public sealed class AesGcmSecretProtector : IConnectionSecretProtector
{
    private const string EnvVarName = "GINKGO_TENANT_DB_KEY";
    private const string ConfigPath = "Database:TenantDbKey";
    private const string MagicPrefix = "ENC1:"; // 前缀用于快速识别密文，便于历史明文兼容

    private readonly byte[] _key;
    private readonly ILogger<AesGcmSecretProtector>? _logger;

    public AesGcmSecretProtector(IConfiguration configuration, ILogger<AesGcmSecretProtector>? logger = null)
    {
        _logger = logger;

        var rawKey = Environment.GetEnvironmentVariable(EnvVarName);
        var keySource = "ENV:" + EnvVarName;
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            rawKey = configuration?[ConfigPath];
            keySource = "CONFIG:" + ConfigPath;
        }

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            rawKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            keySource = "RANDOM(ephemeral)";
            _logger?.LogWarning(
                "[SecretProtector] 未配置 {Env} 或 {Cfg}，已使用随机临时密钥。生产环境必须配置固定密钥，否则重启后历史密文无法解出。",
                EnvVarName, ConfigPath);
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        _logger?.LogDebug("[SecretProtector] 已加载主密钥（来源：{Source}）", keySource);
    }

    public string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext ?? string.Empty;

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        // 拼接：Nonce(12) | Tag(16) | Cipher
        var blob = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, blob, nonce.Length + tag.Length, cipher.Length);
        return MagicPrefix + Convert.ToBase64String(blob);
    }

    public string Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return protectedText ?? string.Empty;
        if (!IsProtected(protectedText))
        {
            // 兼容历史明文：直接原样返回。
            return protectedText!;
        }

        try
        {
            var blob = Convert.FromBase64String(protectedText!.Substring(MagicPrefix.Length));
            if (blob.Length < 12 + 16 + 1)
                throw new CryptographicException("密文长度不足");

            var nonce = new byte[12];
            var tag = new byte[16];
            var cipher = new byte[blob.Length - 28];
            Buffer.BlockCopy(blob, 0, nonce, 0, 12);
            Buffer.BlockCopy(blob, 12, tag, 0, 16);
            Buffer.BlockCopy(blob, 28, cipher, 0, cipher.Length);

            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[SecretProtector] 密文解密失败，已返回空串。");
            return string.Empty;
        }
    }

    public bool IsProtected(string? text)
        => !string.IsNullOrEmpty(text) && text!.StartsWith(MagicPrefix, StringComparison.Ordinal);
}
