namespace Ginkgo.Domain.Users;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task MarkUsedAsync(long id, DateTime usedAtUtc, CancellationToken ct = default);
    Task DeleteExpiredAsync(DateTime nowUtc, CancellationToken ct = default);
}

