using System.Threading;
using SqlSugar;

namespace Ginkgo.Domain.Settings;

/// <summary>
/// 系统配置仓储（领域专用抽象）。
/// </summary>
public interface ISettingsRepository
{
    Task<Setting?> GetAsync(string key, string? @class, CancellationToken ct = default);
    Task<List<Setting>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Setting entity, CancellationToken ct = default);
    Task UpdateAsync(Setting entity, CancellationToken ct = default);
}

