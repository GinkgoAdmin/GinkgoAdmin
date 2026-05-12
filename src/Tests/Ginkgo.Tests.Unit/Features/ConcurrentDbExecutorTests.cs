// 文件功能说明：
// 验证 ConcurrentDbExecutor 的核心行为：
//   - 空/null 操作列表 → 立即返回空结果
//   - Enabled=false → 串行执行，共享同一 client 引用
//   - Enabled=true  → 并发执行，每个操作获得独立 client；受 MaxDegreeOfParallelism 限流
//   - 异常正确传播
//   - CancellationToken 生效

using System.Collections.Concurrent;
using Ginkgo.Infrastructure.Persistence.Features;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace Ginkgo.Tests.Unit.Features;

public sealed class ConcurrentDbExecutorTests
{
    #region 辅助桩

    /// <summary>创建一个不连接真实数据库的 SqlSugarClient 用作标记。</summary>
    private static ISqlSugarClient CreateMarkerClient() =>
        new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.MySql,
            ConnectionString = "Server=__marker__;",
            IsAutoCloseConnection = true,
        });

    /// <summary>IServiceScope 桩：返回固定的 ISqlSugarClient。</summary>
    private sealed class StubScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public StubScope(ISqlSugarClient client) => ServiceProvider = new StubProvider(client);
        public void Dispose() { }
    }

    /// <summary>IServiceProvider 桩：只支持 ISqlSugarClient 解析。</summary>
    private sealed class StubProvider : IServiceProvider
    {
        private readonly ISqlSugarClient _client;
        public StubProvider(ISqlSugarClient client) => _client = client;
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ISqlSugarClient) ? _client : null;
    }

    /// <summary>IServiceScopeFactory 桩：每次 CreateScope 调用工厂委托创建新 client。</summary>
    private sealed class StubScopeFactory : IServiceScopeFactory
    {
        private readonly Func<ISqlSugarClient> _factory;
        public StubScopeFactory(Func<ISqlSugarClient> factory) => _factory = factory;
        public IServiceScope CreateScope() => new StubScope(_factory());
    }

    /// <summary>构造 ConcurrentDbExecutor（串行模式）。</summary>
    private static ConcurrentDbExecutor CreateSerial(ISqlSugarClient sharedClient) =>
        new(sharedClient,
            new StubScopeFactory(CreateMarkerClient),
            new ConcurrencyOptions { Enabled = false });

    /// <summary>构造 ConcurrentDbExecutor（并发模式）。</summary>
    private static ConcurrentDbExecutor CreateParallel(
        ISqlSugarClient sharedClient,
        Func<ISqlSugarClient> scopeClientFactory,
        int maxDop = 4) =>
        new(sharedClient,
            new StubScopeFactory(scopeClientFactory),
            new ConcurrencyOptions { Enabled = true, MaxDegreeOfParallelism = maxDop });

    #endregion

    // ========== 空操作 ==========

    [Fact]
    public async Task RunAsync_Typed_NullOperations_ReturnsEmpty()
    {
        var sut = CreateSerial(CreateMarkerClient());
        var result = await sut.RunAsync<int>(null!);
        Assert.Empty(result);
    }

    [Fact]
    public async Task RunAsync_Typed_EmptyOperations_ReturnsEmpty()
    {
        var sut = CreateSerial(CreateMarkerClient());
        var result = await sut.RunAsync<int>(Array.Empty<Func<ISqlSugarClient, CancellationToken, Task<int>>>());
        Assert.Empty(result);
    }

    [Fact]
    public async Task RunAsync_Void_NullOperations_Completes()
    {
        var sut = CreateSerial(CreateMarkerClient());
        await sut.RunAsync(null!); // 不抛异常即通过
    }

    [Fact]
    public async Task RunAsync_Void_EmptyOperations_Completes()
    {
        var sut = CreateSerial(CreateMarkerClient());
        await sut.RunAsync(Array.Empty<Func<ISqlSugarClient, CancellationToken, Task>>());
    }

    // ========== 串行模式 ==========

    [Fact]
    public async Task Serial_AllOperationsReceiveSameClient()
    {
        var shared = CreateMarkerClient();
        var sut = CreateSerial(shared);
        var receivedClients = new List<ISqlSugarClient>();

        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            (db, _) => { receivedClients.Add(db); return Task.FromResult(1); },
            (db, _) => { receivedClients.Add(db); return Task.FromResult(2); },
            (db, _) => { receivedClients.Add(db); return Task.FromResult(3); },
        };

        var results = await sut.RunAsync<int>(ops);

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 1, 2, 3 }, results);
        // 串行模式所有操作共享同一 client 引用
        Assert.All(receivedClients, c => Assert.Same(shared, c));
    }

    [Fact]
    public async Task Serial_ExecutesInOrder()
    {
        var sut = CreateSerial(CreateMarkerClient());
        var order = new List<int>();

        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            async (_, _) => { order.Add(1); await Task.Yield(); return 1; },
            async (_, _) => { order.Add(2); await Task.Yield(); return 2; },
            async (_, _) => { order.Add(3); await Task.Yield(); return 3; },
        };

        await sut.RunAsync<int>(ops);
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    // ========== 并发模式 ==========

    [Fact]
    public async Task Parallel_EachOperationGetsDistinctClient()
    {
        var shared = CreateMarkerClient();
        var receivedClients = new ConcurrentBag<ISqlSugarClient>();

        var sut = CreateParallel(shared, CreateMarkerClient, maxDop: 4);

        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            async (db, _) => { receivedClients.Add(db); await Task.Delay(10); return 1; },
            async (db, _) => { receivedClients.Add(db); await Task.Delay(10); return 2; },
            async (db, _) => { receivedClients.Add(db); await Task.Delay(10); return 3; },
        };

        var results = await sut.RunAsync<int>(ops);

        Assert.Equal(3, results.Count);
        // 并发模式每个操作获得独立 client（从 scope 工厂创建），不是 shared
        Assert.All(receivedClients, c => Assert.NotSame(shared, c));
        // 每个 client 彼此不同
        Assert.Equal(receivedClients.Count, receivedClients.Distinct().Count());
    }

    [Fact]
    public async Task Parallel_RespectsMaxDop()
    {
        var sut = CreateParallel(CreateMarkerClient(), CreateMarkerClient, maxDop: 2);
        var concurrencyLevel = 0;
        var maxObserved = 0;
        var lockObj = new object();

        var ops = Enumerable.Range(0, 6).Select<int, Func<ISqlSugarClient, CancellationToken, Task<int>>>(i =>
            async (_, ct) =>
            {
                var current = Interlocked.Increment(ref concurrencyLevel);
                lock (lockObj) { if (current > maxObserved) maxObserved = current; }
                await Task.Delay(50, ct);
                Interlocked.Decrement(ref concurrencyLevel);
                return i;
            }).ToArray();

        var results = await sut.RunAsync<int>(ops);

        Assert.Equal(6, results.Count);
        // 最大并发度不应超过 MaxDegreeOfParallelism=2
        Assert.True(maxObserved <= 2, $"观察到的最大并发度 {maxObserved} 超过了限制 2");
    }

    [Fact]
    public async Task Parallel_ResultsAreInInputOrder()
    {
        var sut = CreateParallel(CreateMarkerClient(), CreateMarkerClient, maxDop: 4);

        // 各任务延迟不同，但结果应按输入顺序排列
        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            async (_, ct) => { await Task.Delay(60, ct); return 10; },
            async (_, ct) => { await Task.Delay(10, ct); return 20; },
            async (_, ct) => { await Task.Delay(30, ct); return 30; },
        };

        var results = await sut.RunAsync<int>(ops);
        Assert.Equal(new[] { 10, 20, 30 }, results);
    }

    // ========== 异常传播 ==========

    [Fact]
    public async Task Serial_ExceptionPropagates()
    {
        var sut = CreateSerial(CreateMarkerClient());
        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            (_, _) => throw new InvalidOperationException("test error"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunAsync<int>(ops));
    }

    [Fact]
    public async Task Parallel_ExceptionPropagates()
    {
        var sut = CreateParallel(CreateMarkerClient(), CreateMarkerClient, maxDop: 4);
        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            (_, _) => Task.FromResult(1),
            (_, _) => throw new InvalidOperationException("test error"),
        };

        // Task.WhenAll 会将第一个异常展开
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunAsync<int>(ops));
    }

    // ========== 取消令牌 ==========

    [Fact]
    public async Task Serial_CancellationIsRespected()
    {
        var sut = CreateSerial(CreateMarkerClient());
        using var cts = new CancellationTokenSource();
        var callCount = 0;

        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            (_, _) => { callCount++; cts.Cancel(); return Task.FromResult(1); },
            (_, _) => { callCount++; return Task.FromResult(2); },
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.RunAsync<int>(ops, cts.Token));
        // 第一个执行完后取消，第二个不应执行
        Assert.Equal(1, callCount);
    }

    // ========== MaxDop 兜底 ==========

    [Fact]
    public async Task Parallel_ZeroMaxDop_FallsBackTo4()
    {
        // MaxDegreeOfParallelism=0 时应兜底为 4（不抛异常）
        var sut = CreateParallel(CreateMarkerClient(), CreateMarkerClient, maxDop: 0);
        var ops = new Func<ISqlSugarClient, CancellationToken, Task<int>>[]
        {
            (_, _) => Task.FromResult(42),
        };

        var results = await sut.RunAsync<int>(ops);
        Assert.Single(results);
        Assert.Equal(42, results[0]);
    }
}
