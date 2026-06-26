using Ginkgo.Domain.Settings;
using SqlSugar;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<SettingsRepository>? _logger;
    
    public SettingsRepository(ISqlSugarClient db, ILogger<SettingsRepository>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Setting?> GetAsync(string key, string? @class, CancellationToken ct = default)
    {
        // 注意：当前数据库主键为 Key；必须按 Key 唯一查询，避免当 Class 不同却尝试新增导致主键冲突
        return await _db.Queryable<Setting>().Where(s => s.Key == key).FirstAsync();
    }

    public async Task<List<Setting>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Queryable<Setting>().OrderBy(s => s.Key).ToListAsync();
    }

    public async Task<List<Setting>> GetByModuleAsync(string module, CancellationToken ct = default)
    {
        return await _db.Queryable<Setting>()
            .Where(s => s.Module == module)
            .OrderBy(s => s.Key)
            .ToListAsync();
    }

    public async Task AddAsync(Setting entity, CancellationToken ct = default)
    {
        await _db.Insertable(entity).ExecuteCommandAsync();
    }

    public async Task UpdateAsync(Setting entity, CancellationToken ct = default)
    {
        // 注意：ginkgo_Sys_Settings 表的主键是 Key，不是 Id
        // 使用 SqlSugar 的 Updateable 方法，自动处理不同数据库的语法差异
        var affected = await _db.Updateable(entity)
            .UpdateColumns(s => new { 
                s.Value, 
                s.Type, 
                s.Description, 
                s.Version, 
                s.UpdatedAt, 
                s.UpdatedBy, 
                s.Class,
                s.Module,
                s.Id 
            })
            .Where(s => s.Key == entity.Key)
            .ExecuteCommandAsync();
        
        _logger?.LogDebug("Update affected {Affected} rows for Key={Key}", affected, entity.Key);
        
        if (affected == 0)
        {
            throw new InvalidOperationException($"更新配置失败：Key '{entity.Key}' 不存在");
        }
    }

    public async Task<int> DeleteByKeysAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        var list = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();
        if (list.Count == 0) return 0;
        return await _db.Deleteable<Setting>().Where(s => list.Contains(s.Key)).ExecuteCommandAsync();
    }

    public async Task<int> DeleteByModuleAndKeyPrefixAsync(string module, string keyPrefix, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(keyPrefix)) return 0;
        var all = await GetByModuleAsync(module, ct);
        var keys = all
            .Where(s => s.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
            .Select(s => s.Key)
            .ToList();
        return await DeleteByKeysAsync(keys, ct);
    }
}

