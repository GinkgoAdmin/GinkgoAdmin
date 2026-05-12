using Ginkgo.Domain.Verification;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar.Verification;

/// <summary>
/// 验证码模板仓储实现（SqlSugar）。
/// </summary>
public sealed class VerificationTemplateRepository : IVerificationTemplateRepository
{
	private readonly ISqlSugarClient _db;

	public VerificationTemplateRepository(ISqlSugarClient db) => _db = db;

	/// <summary>获取指定用途和渠道的默认模板</summary>
	public async Task<VerificationTemplate?> GetDefaultAsync(int purpose, int channel, CancellationToken ct = default)
	{
		return await _db.Queryable<VerificationTemplate>()
			.Where(x => x.Purpose == purpose && x.Channel == channel && x.IsDefault && x.Enabled)
			.FirstAsync();
	}

	/// <summary>获取所有模板</summary>
	public async Task<List<VerificationTemplate>> GetAllAsync(CancellationToken ct = default)
	{
		return await _db.Queryable<VerificationTemplate>()
			.OrderBy(x => x.Purpose)
			.OrderBy(x => x.Channel)
			.OrderBy(x => x.SortOrder)
			.ToListAsync();
	}

	/// <summary>根据ID获取模板</summary>
	public async Task<VerificationTemplate?> GetByIdAsync(long id, CancellationToken ct = default)
	{
		return await _db.Queryable<VerificationTemplate>()
			.Where(x => x.Id == id)
			.FirstAsync();
	}

	/// <summary>新增模板</summary>
	public Task AddAsync(VerificationTemplate template, CancellationToken ct = default)
	{
		return _db.Insertable(template).ExecuteCommandAsync();
	}

	/// <summary>更新模板</summary>
	public Task UpdateAsync(VerificationTemplate template, CancellationToken ct = default)
	{
		template.UpdatedAt = DateTime.Now;
		return _db.Updateable(template).ExecuteCommandAsync();
	}

	/// <summary>取消指定 Purpose+Channel 组中其他模板的默认标记</summary>
	public Task ClearDefaultAsync(int purpose, int channel, long excludeId, CancellationToken ct = default)
	{
		return _db.Updateable<VerificationTemplate>()
			.SetColumns(x => x.IsDefault == false)
			.SetColumns(x => x.UpdatedAt == DateTime.Now)
			.Where(x => x.Purpose == purpose && x.Channel == channel && x.Id != excludeId && x.IsDefault)
			.ExecuteCommandAsync();
	}

	/// <summary>删除模板</summary>
	public Task DeleteAsync(long id, CancellationToken ct = default)
	{
		return _db.Deleteable<VerificationTemplate>()
			.Where(x => x.Id == id)
			.ExecuteCommandAsync();
	}
}
