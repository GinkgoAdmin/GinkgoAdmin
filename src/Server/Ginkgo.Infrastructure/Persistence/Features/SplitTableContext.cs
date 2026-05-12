// 文件功能说明：
// ISplitTableContext 默认实现。封装 SqlSugar 的 .SplitTable() 链式调用，
// 受 db.json Database.Features.SplitTable.Enabled 开关控制。
// Enabled=false 时所有操作抛 NotSupportedException。

using Ginkgo.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Linq.Expressions;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 分表操作上下文默认实现。
/// </summary>
public sealed class SplitTableContext : ISplitTableContext
{
    private readonly ISqlSugarClient _client;
    private readonly SplitTableOptions _options;
    private readonly ILogger<SplitTableContext>? _logger;

    public SplitTableContext(
        ISqlSugarClient client,
        IOptions<DatabaseFeaturesOptions> features,
        ILogger<SplitTableContext>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = features?.Value?.SplitTable ?? new SplitTableOptions();
        _logger = logger;
    }

    /// <summary>供单测注入自定义选项。</summary>
    internal SplitTableContext(
        ISqlSugarClient client,
        SplitTableOptions options,
        ILogger<SplitTableContext>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new SplitTableOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public async Task<int> InsertAsync<T>(T entity) where T : class, new()
    {
        EnsureEnabled();
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        return await _client.Insertable(entity).SplitTable().ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public async Task<int> InsertAsync<T>(List<T> entities) where T : class, new()
    {
        EnsureEnabled();
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        if (entities.Count == 0) return 0;
        return await _client.Insertable(entities).SplitTable().ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public ISugarQueryable<T> QueryByRange<T>(DateTime start, DateTime end) where T : class, new()
    {
        EnsureEnabled();
        return _client.Queryable<T>().SplitTable(start, end);
    }

    /// <inheritdoc />
    public async Task<int> UpdateAsync<T>(T entity) where T : class, new()
    {
        EnsureEnabled();
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        return await _client.Updateable(entity).SplitTable().ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync<T>(
        Expression<Func<T, bool>> predicate) where T : class, new()
    {
        EnsureEnabled();
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return await _client.Deleteable<T>()
            .Where(predicate)
            .SplitTable(tabs => tabs)
            .ExecuteCommandAsync();
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
        {
            throw new NotSupportedException(
                "SplitTable 分表功能未启用。请在 db.json 设置 Database.Features.SplitTable.Enabled = true，" +
                "并在实体上配置 [SplitTable(SplitType.xxx)] + [SplitField] 特性。");
        }
    }
}
