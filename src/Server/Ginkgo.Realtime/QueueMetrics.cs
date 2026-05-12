using System.Threading;

namespace Ginkgo.Realtime;

/// <summary>
/// 队列度量指标接口。
/// </summary>
public interface IQueueMetrics
{
    long Published { get; }
    long Consumed { get; }
    long Failed { get; }
    long DeadLettered { get; }
    long Retried { get; }
}

/// <summary>
/// 线程安全的队列度量计数器。
/// </summary>
public sealed class QueueMetrics : IQueueMetrics
{
    private long _published;
    private long _consumed;
    private long _failed;
    private long _deadLettered;
    private long _retried;

    public long Published => Interlocked.Read(ref _published);
    public long Consumed => Interlocked.Read(ref _consumed);
    public long Failed => Interlocked.Read(ref _failed);
    public long DeadLettered => Interlocked.Read(ref _deadLettered);
    public long Retried => Interlocked.Read(ref _retried);

    public void IncrementPublished() => Interlocked.Increment(ref _published);
    public void IncrementConsumed() => Interlocked.Increment(ref _consumed);
    public void IncrementFailed() => Interlocked.Increment(ref _failed);
    public void IncrementDeadLettered() => Interlocked.Increment(ref _deadLettered);
    public void IncrementRetried() => Interlocked.Increment(ref _retried);
}
