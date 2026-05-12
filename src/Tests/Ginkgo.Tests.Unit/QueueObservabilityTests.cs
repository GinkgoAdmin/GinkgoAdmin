using Ginkgo.Realtime;

namespace Ginkgo.Tests.Unit;

/// <summary>
/// InMemoryQueue 可观测性测试：验证重试、死信和度量指标。
/// </summary>
public class QueueObservabilityTests
{
    [Fact]
    public async Task Publish_IncreasesPublishedCount()
    {
        var metrics = new QueueMetrics();
        var queue = new InMemoryQueue(metrics);

        await queue.PublishAsync("test-topic", new { msg = "hello" });

        Assert.Equal(1, metrics.Published);
        Assert.Equal(0, metrics.Consumed);
    }

    [Fact]
    public async Task Subscribe_SuccessfulHandler_IncreasesConsumedCount()
    {
        var metrics = new QueueMetrics();
        var queue = new InMemoryQueue(metrics);

        var cts = new CancellationTokenSource();
        var consumed = new TaskCompletionSource<bool>();

        var subscribeTask = Task.Run(async () =>
        {
            await queue.SubscribeAsync("test-topic", async body =>
            {
                consumed.TrySetResult(true);
                await Task.CompletedTask;
            }, cts.Token);
        });

        await queue.PublishAsync("test-topic", new { msg = "hello" });
        await Task.WhenAny(consumed.Task, Task.Delay(5000));

        cts.Cancel();
        Assert.True(consumed.Task.IsCompleted);
        Assert.Equal(1, metrics.Published);
        Assert.Equal(1, metrics.Consumed);
        Assert.Equal(0, metrics.Failed);
    }

    [Fact]
    public async Task Subscribe_FailingHandler_RetriesAndDeadLetters()
    {
        var metrics = new QueueMetrics();
        var queue = new InMemoryQueue(metrics);

        var cts = new CancellationTokenSource();
        var attempts = 0;
        var deadLetterReceived = new TaskCompletionSource<bool>();

        var subscribeTask = Task.Run(async () =>
        {
            await queue.SubscribeAsync("fail-topic", body =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("Simulated failure");
            }, cts.Token);
        });

        // 监听死信
        var dlqTask = Task.Run(async () =>
        {
            await foreach (var item in queue.DeadLetterReader.ReadAllAsync(cts.Token))
            {
                deadLetterReceived.TrySetResult(true);
                break;
            }
        });

        await queue.PublishAsync("fail-topic", new { msg = "will-fail" });
        await Task.WhenAny(deadLetterReceived.Task, Task.Delay(15000));

        cts.Cancel();
        var deadLetterResult = await deadLetterReceived.Task;
        Assert.True(deadLetterReceived.Task.IsCompleted && deadLetterResult);
        Assert.Equal(1, metrics.DeadLettered);
        // 初始 1 次 + 3 次重试 = 4 次 handler 调用
        Assert.Equal(4, attempts);
        Assert.True(metrics.Failed >= 4);
        Assert.True(metrics.Retried >= 3);
    }

    [Fact]
    public void QueueMetrics_ThreadSafe_IncrementOperations()
    {
        var metrics = new QueueMetrics();

        Parallel.For(0, 1000, _ => metrics.IncrementPublished());
        Parallel.For(0, 500, _ => metrics.IncrementConsumed());
        Parallel.For(0, 200, _ => metrics.IncrementFailed());

        Assert.Equal(1000, metrics.Published);
        Assert.Equal(500, metrics.Consumed);
        Assert.Equal(200, metrics.Failed);
    }
}
