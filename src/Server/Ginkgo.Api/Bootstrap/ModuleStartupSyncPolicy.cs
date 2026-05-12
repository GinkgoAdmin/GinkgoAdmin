using Ginkgo.Domain.Modules;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 模块启动同步策略。
/// </summary>
public sealed class ModuleStartupSyncPolicy
{
    private readonly Dictionary<string, InstalledModuleEntity> _records;

    private ModuleStartupSyncPolicy(IEnumerable<InstalledModuleEntity> records)
    {
        _records = records
            .GroupBy(x => x.ModuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);
    }

    public static ModuleStartupSyncPolicy FromDatabase(IEnumerable<InstalledModuleEntity> records) => new(records);

    /// <summary>
    /// 数据库已有软删除记录时，磁盘残留目录不能在启动扫描时重新同步为已安装。
    /// </summary>
    public bool ShouldSynchronize(string moduleId)
        => !_records.TryGetValue(moduleId, out var record) || !record.IsDeleted;

    public bool ResolveEnabled(string moduleId)
    {
        if (!_records.TryGetValue(moduleId, out var record))
        {
            return true;
        }

        return record.Enabled && !record.IsDeleted;
    }
}
