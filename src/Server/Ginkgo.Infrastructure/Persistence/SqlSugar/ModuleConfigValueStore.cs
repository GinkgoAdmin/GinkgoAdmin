using Ginkgo.Domain.Settings;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar;

/// <summary>
/// 插件数据库存储模式下的配置值读取实现。
/// </summary>
public sealed class ModuleConfigValueStore : IModuleConfigValueStore
{
    private readonly ISettingsRepository _repo;

    public ModuleConfigValueStore(ISettingsRepository repo)
    {
        _repo = repo;
    }

    public async Task<string?> GetValueAsync(string moduleId, string configFile, string itemName, CancellationToken ct = default)
    {
        var key = ModuleConfigKeys.Build(moduleId, configFile, itemName);
        var setting = await _repo.GetAsync(key, null, ct);
        return setting?.Value;
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetAllValuesAsync(string moduleId, string configFile, CancellationToken ct = default)
    {
        var prefix = $"{moduleId}:{configFile}:";
        var list = await _repo.GetByModuleAsync(moduleId, ct);
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in list)
        {
            if (!s.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            dict[s.Key[prefix.Length..]] = s.Value;
        }
        return dict;
    }
}
