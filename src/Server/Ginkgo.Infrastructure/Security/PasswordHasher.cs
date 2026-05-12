using System.Security.Cryptography;
using System.Text;
using Ginkgo.Domain.Users;

namespace Ginkgo.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    // PBKDF2 参数（可根据需要从配置加载）
    private const int Iterations = 100_000;
    private const int SaltSize = 16; // 128-bit
    private const int KeySize = 32;  // 256-bit

    public string Hash(string rawPassword, out string? salt)
    {
        salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize));
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(rawPassword),
            Convert.FromBase64String(salt),
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string rawPassword, string hash, string? salt)
    {
        if (string.IsNullOrEmpty(salt)) return false;
        var computed = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(rawPassword),
            Convert.FromBase64String(salt),
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(hash),
            computed);
    }
}

