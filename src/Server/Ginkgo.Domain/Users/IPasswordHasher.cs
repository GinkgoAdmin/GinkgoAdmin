namespace Ginkgo.Domain.Users;

public interface IPasswordHasher
{
    string Hash(string rawPassword, out string? salt);
    bool Verify(string rawPassword, string hash, string? salt);
}

