// 文件功能说明：
// IConcurrentDbExecutor 的默认实现。根据 db.json 的 Database.Features.Concurrency 开关：
// - Enabled=false：串行执行所有操作，共享同一 ISqlSugarClient，零额外开销。
// - Enabled=true：每个操作通过 IServiceScopeFactory 获取独立 ISqlSugarClient 实例，
//   使用 SemaphoreSlim 限流并发度（MaxDegreeOfParallelism），避免连接池耗尽。
//
// 选择 IServiceScopeFactory（而非 CopyNew）的原因：
// 1. 通过 DI scope 拿到的 ISqlSugarClient 天然包含完整的 AOP / QueryFilter / 模块配置器；
//    CopyNew 只克隆连接配置，不携带 Aop.OnLogExecuting / OnError / ISqlSugarConfigurator 挂载。
// 2. 单测可直接 mock IServiceScopeFactory，无需真实数据库连接。

using Ginkgo.Infrastructure.Abstractions;
using Ginkgo.Infrastructure.Persistence.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 并发数据库操作执行器默认实现。
/// </summary>
public sealed class ConcurrentDbExecutor : IConcurrentDbExecutor
{
    private readonly ISqlSugarClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrencyOptions _options;
    private readonly ILogger<ConcurrentDbExecutor>? _logger;

    public ConcurrentDbExecutor(
        ISqlSugarClient client,
        IServiceScopeFactory scopeFactory,
        IOptions<DatabaseFeaturesOptions> features,
        ILogger<ConcurrentDbExecutor>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = features?.Value?.Concurrency ?? new ConcurrencyOptions();
        _logger = logger;
    }

    /// <summary>
    /// 供单测注入自定义选项的内部构造函数。
    /// </summary>
    internal ConcurrentDbExecutor(
        ISqlSugarClient client,
        IServiceScopeFactory scopeFactory,
        ConcurrencyOptions options,
        ILogger<ConcurrentDbExecutor>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? new ConcurrencyOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TResult>> RunAsync<TResult>(
        IReadOnlyList<Func<ISqlSugarClient, CancellationToken, Task<TResult>>> operations,
        CancellationToken ct = default)
    {
        if (operations == null || operations.Count == 0)
        {
            return Array.Empty<TResult>();
        }

        if (!_options.Enabled)
        {
            // 串行模式：共享同一 client，按序逐个执行。
            var results = new TResult[operations.Count];
            for (var i = 0; i < operations.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                results[i] = await operations[i](_client, ct).ConfigureAwait(false);
            }
            return results;
        }

        // 并发模式：每个操作获取独立 DI scope 中的 ISqlSugarClient，SemaphoreSlim 限流。
        var maxDop = _options.MaxDegreeOfParallelism > 0 ? _options.MaxDegreeOfParallelism : 4;
        _logger?.LogDebug(
            "[Features.Concurrency] 并发执行 {Count} 个操作，MaxDegreeOfParallelism={MaxDop}",
            operations.Count, maxDop);

        using var semaphore = new SemaphoreSlim(maxDop, maxDop);
        var tasks = new Task<TResult>[operations.Count];

        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            tasks[i] = RunWithSemaphoreAsync(semaphore, op, ct);
        }

        var allResults = await Task.WhenAll(tasks).ConfigureAwait(false);
        return allResults;
    }

    /// <inheritdoc />
    public async Task RunAsync(
        IReadOnlyList<Func<ISqlSugarClient, CancellationToken, Task>> operations,
        CancellationToken ct = default)
    {
        if (operations == null || operations.Count == 0)
        {
            return;
        }

        // 包装为有返回值的委托，复用核心逻辑。
        var wrapped = new Func<ISqlSugarClient, CancellationToken, Task<bool>>[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            wrapped[i] = async (db, token) =>
            {
                await op(db, token).ConfigureAwait(false);
                return true;
            };
        }

        await RunAsync<bool>(wrapped, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 在信号量限流下执行单个操作（独立 DI scope）。
    /// </summary>
    private async Task<TResult> RunWithSemaphoreAsync<TResult>(
        SemaphoreSlim semaphore,
        Func<ISqlSugarClient, CancellationToken, Task<TResult>> operation,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 每个并发操作创建独立 DI scope 以获取独立 ISqlSugarClient 实例。
            using var scope = _scopeFactory.CreateScope();
            var scopedClient = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            return await operation(scopedClient, ct).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
