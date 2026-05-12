// 文件功能说明：
// 基于 SqlSugar 的定时任务仓储实现，提供任务记录与执行日志的数据库访问。

using Ginkgo.Domain.Scheduling;
using SqlSugar;

namespace Ginkgo.Infrastructure.Scheduling;

/// <summary>
/// 定时任务仓储实现。
/// </summary>
public sealed class ScheduledTaskRepository : IScheduledTaskRepository
{
    private readonly ISqlSugarClient _db;

    public ScheduledTaskRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    // ===== 任务记录 =====

    public async Task<List<ScheduledTaskRecord>> GetAllTasksAsync(CancellationToken ct = default)
    {
        return await _db.Queryable<ScheduledTaskRecord>()
            .OrderBy(t => t.Group)
            .OrderBy(t => t.TaskKey)
            .ToListAsync(ct);
    }

    public async Task<ScheduledTaskRecord?> GetByKeyAsync(string taskKey, CancellationToken ct = default)
    {
        return await _db.Queryable<ScheduledTaskRecord>()
            .Where(t => t.TaskKey == taskKey)
            .FirstAsync(ct);
    }

    public async Task UpsertAsync(ScheduledTaskRecord record, CancellationToken ct = default)
    {
        var existing = await GetByKeyAsync(record.TaskKey, ct);
        if (existing == null)
        {
            // 首次注册：使用代码中的默认值
            record.Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId();
            record.CreatedAt = DateTime.Now;
            await _db.Insertable(record).ExecuteCommandAsync(ct);
        }
        else
        {
            // 已存在：仅更新元数据字段，不覆盖用户自定义的 CronExpression/IsEnabled
            existing.DisplayName = record.DisplayName;
            existing.Group = record.Group;
            existing.Description = record.Description;
            existing.ExecutionType = record.ExecutionType;
            existing.ExecutionTarget = record.ExecutionTarget;
            existing.Source = record.Source;
            existing.UpdatedAt = DateTime.Now;
            await _db.Updateable(existing)
                .UpdateColumns(e => new
                {
                    e.DisplayName,
                    e.Group,
                    e.Description,
                    e.ExecutionType,
                    e.ExecutionTarget,
                    e.Source,
                    e.UpdatedAt
                })
                .ExecuteCommandAsync(ct);
        }
    }

    public async Task UpdateRunInfoAsync(string taskKey, DateTime? lastRunAt, DateTime? nextRunAt, string? lastResult, int? lastElapsedMs, CancellationToken ct = default)
    {
        await _db.Updateable<ScheduledTaskRecord>()
            .SetColumns(t => new ScheduledTaskRecord
            {
                LastRunAt = lastRunAt,
                NextRunAt = nextRunAt,
                LastResult = lastResult,
                LastElapsedMs = lastElapsedMs,
                UpdatedAt = DateTime.Now
            })
            .Where(t => t.TaskKey == taskKey)
            .ExecuteCommandAsync(ct);
    }

    public async Task UpdateConfigAsync(string taskKey, bool isEnabled, string cronExpression, string? description, CancellationToken ct = default)
    {
        await _db.Updateable<ScheduledTaskRecord>()
            .SetColumns(t => new ScheduledTaskRecord
            {
                IsEnabled = isEnabled,
                CronExpression = cronExpression,
                Description = description,
                UpdatedAt = DateTime.Now
            })
            .Where(t => t.TaskKey == taskKey)
            .ExecuteCommandAsync(ct);
    }

    public async Task CreateDynamicTaskAsync(ScheduledTaskRecord record, CancellationToken ct = default)
    {
        record.Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId();
        record.CreatedAt = DateTime.Now;
        record.DefinitionType = "Dynamic";
        await _db.Insertable(record).ExecuteCommandAsync(ct);
    }

    public async Task<bool> DeleteDynamicTaskAsync(string taskKey, CancellationToken ct = default)
    {
        var deleted = await _db.Deleteable<ScheduledTaskRecord>()
            .Where(t => t.TaskKey == taskKey && t.DefinitionType == "Dynamic")
            .ExecuteCommandAsync(ct);
        return deleted > 0;
    }

    public async Task UpdateDynamicTaskAsync(ScheduledTaskRecord record, CancellationToken ct = default)
    {
        record.UpdatedAt = DateTime.Now;
        await _db.Updateable(record)
            .UpdateColumns(e => new
            {
                e.DisplayName,
                e.Group,
                e.CronExpression,
                e.IsEnabled,
                e.Description,
                e.ExecutionType,
                e.ExecutionTarget,
                e.ExecutionSource,
                e.ActionKey,
                e.ConfigJson,
                e.UpdatedAt
            })
            .Where(t => t.TaskKey == record.TaskKey && t.DefinitionType == "Dynamic")
            .ExecuteCommandAsync(ct);
    }

    // ===== 执行日志 =====

    public async Task AddLogAsync(ScheduledTaskLog log, CancellationToken ct = default)
    {
        log.Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId();
        await _db.Insertable(log).ExecuteCommandAsync(ct);
    }

    public async Task<(List<ScheduledTaskLog> Items, int Total)> GetLogsPagedAsync(string taskKey, int page, int pageSize, CancellationToken ct = default)
    {
        var total = new RefAsync<int>();
        var items = await _db.Queryable<ScheduledTaskLog>()
            .Where(l => l.TaskKey == taskKey)
            .OrderByDescending(l => l.StartedAt)
            .ToPageListAsync(page, pageSize, total, ct);
        return (items, total.Value);
    }
}
