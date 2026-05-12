// 文件功能说明：
// 慢查询事件上报缓冲。框架 SqlSugar OnLogExecuted 回调内调用 Enqueue 将慢 SQL 投递到内部 Channel；
// 由 SlowQueryHostedService 在后台异步消费并落入操作日志（OpLog）。
//
// 设计要点：
// - 单例 Channel<SlowQueryEvent>（容量上限 + 丢弃最旧策略），避免因 OpLog 写库慢拖累 SQL 链路。
// - Enqueue 是同步、非阻塞的 TryWrite；缓冲已满时直接丢弃最旧事件并自增 DroppedCount。
// - HostedService 通过 ChannelReader.ReadAllAsync 拉取、按租户/上下文落库；遇异常仅记录、不抛出。

using System.Threading.Channels;

namespace Ginkgo.Infrastructure.Persistence.Features;

/// <summary>
/// 慢查询事件载荷。
/// </summary>
public sealed record SlowQueryEvent(
    DateTime At,
    string Sql,
    long ElapsedMs,
    int ThresholdMs,
    long? UserId);

/// <summary>
/// 慢查询事件上报缓冲（单例）。仅当 <c>Database.Features.SlowQuery.WriteToOpLog=true</c> 时由 ApplySlowQuery 调用。
/// </summary>
public sealed class SlowQueryReporter
{
    /// <summary>队列容量上限；超出后写入新事件会丢弃最旧未消费事件。</summary>
    public const int Capacity = 1024;

    private readonly Channel<SlowQueryEvent> _channel;
    private long _droppedCount;

    public SlowQueryReporter()
    {
        _channel = Channel.CreateBounded<SlowQueryEvent>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // 永不阻塞写入：缓冲满时丢弃最旧事件
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>事件读取通道（供 HostedService 消费）。</summary>
    public ChannelReader<SlowQueryEvent> Reader => _channel.Reader;

    /// <summary>因缓冲满而被丢弃的事件累计数。可用于诊断慢查询风暴。</summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>
    /// 入队一个慢查询事件。永远立即返回；缓冲满时静默丢弃并自增 <see cref="DroppedCount"/>。
    /// </summary>
    public void Enqueue(SlowQueryEvent ev)
    {
        if (!_channel.Writer.TryWrite(ev))
        {
            Interlocked.Increment(ref _droppedCount);
        }
    }

    /// <summary>关闭通道（应用关停时调用）。</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
