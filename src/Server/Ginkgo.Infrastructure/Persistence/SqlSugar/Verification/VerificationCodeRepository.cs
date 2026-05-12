using Ginkgo.Domain.Verification;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar.Verification;

/// <summary>
/// 验证码记录仓储实现（SqlSugar）。
/// </summary>
public sealed class VerificationCodeRepository : IVerificationCodeRepository
{
	private readonly ISqlSugarClient _db;

	public VerificationCodeRepository(ISqlSugarClient db) => _db = db;

	/// <summary>新增验证码记录</summary>
	public Task AddAsync(VerificationCode record, CancellationToken ct = default)
	{
		return _db.Insertable(record).ExecuteCommandAsync();
	}

	/// <summary>获取指定目标和用途的最新有效验证码记录</summary>
	public async Task<VerificationCode?> GetLatestAsync(string target, int purpose, CancellationToken ct = default)
	{
		var now = DateTime.Now;
		return await _db.Queryable<VerificationCode>()
			.Where(x => x.Target == target && x.Purpose == purpose)
			.Where(x => x.ExpiresAt > now && x.VerifiedAt == null)
			.OrderByDescending(x => x.CreatedAt)
			.FirstAsync();
	}

	/// <summary>更新验证码记录</summary>
	public Task UpdateAsync(VerificationCode record, CancellationToken ct = default)
	{
		return _db.Updateable(record).ExecuteCommandAsync();
	}

	/// <summary>清理过期记录</summary>
	public Task DeleteExpiredAsync(DateTime before, CancellationToken ct = default)
	{
		return _db.Deleteable<VerificationCode>()
			.Where(x => x.ExpiresAt < before)
			.ExecuteCommandAsync();
	}
}
