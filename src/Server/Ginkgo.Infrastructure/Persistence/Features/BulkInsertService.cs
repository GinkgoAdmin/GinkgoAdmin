// 文件功能说明：
// IBulkInsertService 的默认 SqlSugar 实现。包装 Fastest<T>().BulkCopy 与 BulkUpdate；
// 当 Database.Features.BulkOps.Enabled=false 时降级为逐行 Insertable / Updateable，保证降级可用。
//
// 注意：
// - BulkCopy 不会触发 IEntityChangeInterceptor、不会应用 QueryFilter（路径不同），调用方需要自行处理 TenantId 等审计字段。
// - DataTable 路径用于 ImportController 的"动态列导入"场景；表名与列名都假设由调用方做过白名单校验。

using System.Data;
using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// IBulkInsertService 的默认实现（基于 SqlSugar Fastest&lt;T&gt;）。
/// </summary>
public sealed class BulkInsertService : IBulkInsertService
{
    private readonly ISqlSugarClient _db;
    private readonly DatabaseFeaturesOptions _features;
    private readonly ILogger<BulkInsertService>? _logger;

    public BulkInsertService(
        ISqlSugarClient db,
        IOptions<DatabaseFeaturesOptions> features,
        ILogger<BulkInsertService>? logger = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _features = features?.Value ?? new DatabaseFeaturesOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> BulkInsertAsync<T>(
        IList<T> entities,
        int? batchSize = null,
        CancellationToken ct = default) where T : class, new()
    {
        if (entities == null || entities.Count == 0) return 0;
        ct.ThrowIfCancellationRequested();

        var size = ResolveBatchSize(batchSize);

        if (_features.BulkOps.Enabled)
        {
            // 启用大数据写入：使用 Fastest<T>.BulkCopy（最快路径）。SqlSugar 要求 List<T>，调用方传 IList 时需一次 ToList。
            var list = entities as List<T> ?? entities.ToList();
            return await _db.Fastest<T>().PageSize(size).BulkCopyAsync(list).ConfigureAwait(false);
        }

        // 降级路径：逐批 Insertable.ExecuteCommandAsync。同样不触发 AOP/QueryFilter，但行为更接近"逐行插入"。
        _logger?.LogDebug(
            "[BulkInsertService] BulkOps 已关闭，降级为 Insertable 逐批插入；本次 {Count} 行 / 每批 {BatchSize}。",
            entities.Count, size);

        var total = 0;
        for (var offset = 0; offset < entities.Count; offset += size)
        {
            ct.ThrowIfCancellationRequested();
            var slice = entities.Skip(offset).Take(size).ToList();
            total += await _db.Insertable(slice).ExecuteCommandAsync().ConfigureAwait(false);
        }
        return total;
    }

    /// <inheritdoc />
    public async Task<int> BulkInsertDataTableAsync(
        string tableName,
        DataTable data,
        int? batchSize = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("表名不能为空。", nameof(tableName));
        if (data == null || data.Rows.Count == 0) return 0;
        ct.ThrowIfCancellationRequested();

        var size = ResolveBatchSize(batchSize);

        if (_features.BulkOps.Enabled)
        {
            return await _db.Fastest<DataTable>().PageSize(size).AS(tableName).BulkCopyAsync(data).ConfigureAwait(false);
        }

        // 降级路径：把 DataTable 拆批用 Insertable<Dictionary<string, object?>>().AS(tableName) 写入。
        _logger?.LogDebug(
            "[BulkInsertService] BulkOps 已关闭，降级为 Insertable<Dictionary> 写入到表 {Table}；本次 {Count} 行 / 每批 {BatchSize}。",
            tableName, data.Rows.Count, size);

        var rows = new List<Dictionary<string, object?>>(data.Rows.Count);
        foreach (DataRow row in data.Rows)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in data.Columns)
            {
                var val = row[col];
                dict[col.ColumnName] = val == DBNull.Value ? null : val;
            }
            rows.Add(dict);
        }

        var total = 0;
        for (var offset = 0; offset < rows.Count; offset += size)
        {
            ct.ThrowIfCancellationRequested();
            var slice = rows.Skip(offset).Take(size).ToList();
            total += await _db.Insertable(slice).AS(tableName).ExecuteCommandAsync().ConfigureAwait(false);
        }
        return total;
    }

    /// <inheritdoc />
    public async Task<int> BulkUpdateAsync<T>(
        IList<T> entities,
        int? batchSize = null,
        CancellationToken ct = default) where T : class, new()
    {
        if (entities == null || entities.Count == 0) return 0;
        ct.ThrowIfCancellationRequested();

        var size = ResolveBatchSize(batchSize);

        if (_features.BulkOps.Enabled)
        {
            var list = entities as List<T> ?? entities.ToList();
            return await _db.Fastest<T>().PageSize(size).BulkUpdateAsync(list).ConfigureAwait(false);
        }

        _logger?.LogDebug(
            "[BulkInsertService] BulkOps 已关闭，降级为 Updateable 逐批更新；本次 {Count} 行 / 每批 {BatchSize}。",
            entities.Count, size);

        var total = 0;
        for (var offset = 0; offset < entities.Count; offset += size)
        {
            ct.ThrowIfCancellationRequested();
            var slice = entities.Skip(offset).Take(size).ToList();
            total += await _db.Updateable(slice).ExecuteCommandAsync().ConfigureAwait(false);
        }
        return total;
    }

    /// <summary>
    /// 计算实际批次大小：调用方显式传入 > 配置 DefaultBatchSize > 兜底 5000。
    /// </summary>
    private int ResolveBatchSize(int? requested)
    {
        if (requested is { } r && r > 0) return r;
        var d = _features.BulkOps.DefaultBatchSize;
        return d > 0 ? d : 5000;
    }
}
