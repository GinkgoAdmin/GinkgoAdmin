namespace Ginkgo.Domain.Verification;

/// <summary>
/// 验证码记录仓储接口。
/// </summary>
public interface IVerificationCodeRepository
{
	/// <summary>新增验证码记录</summary>
	Task AddAsync(VerificationCode record, CancellationToken ct = default);

	/// <summary>获取指定目标和用途的最新有效验证码记录</summary>
	Task<VerificationCode?> GetLatestAsync(string target, int purpose, CancellationToken ct = default);

	/// <summary>更新验证码记录（校验次数、验证时间等）</summary>
	Task UpdateAsync(VerificationCode record, CancellationToken ct = default);

	/// <summary>清理过期记录</summary>
	Task DeleteExpiredAsync(DateTime before, CancellationToken ct = default);
}
