namespace Ginkgo.Domain.Verification;

/// <summary>
/// 验证码模板仓储接口。
/// </summary>
public interface IVerificationTemplateRepository
{
	/// <summary>获取指定用途和渠道的默认模板</summary>
	Task<VerificationTemplate?> GetDefaultAsync(int purpose, int channel, CancellationToken ct = default);

	/// <summary>获取所有模板</summary>
	Task<List<VerificationTemplate>> GetAllAsync(CancellationToken ct = default);

	/// <summary>根据ID获取模板</summary>
	Task<VerificationTemplate?> GetByIdAsync(long id, CancellationToken ct = default);

	/// <summary>新增模板</summary>
	Task AddAsync(VerificationTemplate template, CancellationToken ct = default);

	/// <summary>更新模板</summary>
	Task UpdateAsync(VerificationTemplate template, CancellationToken ct = default);

	/// <summary>取消指定 Purpose+Channel 组中其他模板的默认标记（保留 excludeId）</summary>
	Task ClearDefaultAsync(int purpose, int channel, long excludeId, CancellationToken ct = default);

	/// <summary>删除模板</summary>
	Task DeleteAsync(long id, CancellationToken ct = default);
}
