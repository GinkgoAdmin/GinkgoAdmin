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
    /// <summary>按模块标识查询配置项（用于插件数据库存储模式）。</summary>
    Task<List<Setting>> GetByModuleAsync(string module, CancellationToken ct = default);
    Task AddAsync(Setting entity, CancellationToken ct = default);
    Task UpdateAsync(Setting entity, CancellationToken ct = default);
    /// <summary>按主键批量删除配置项。</summary>
    Task<int> DeleteByKeysAsync(IEnumerable<string> keys, CancellationToken ct = default);
    /// <summary>删除指定模块下键名以给定前缀开头的配置项（用于插件配置从库移除）。</summary>
    Task<int> DeleteByModuleAndKeyPrefixAsync(string module, string keyPrefix, CancellationToken ct = default);
}

