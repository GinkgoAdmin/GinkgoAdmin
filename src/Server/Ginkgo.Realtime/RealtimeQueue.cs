using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ginkgo.Realtime;

/// <summary>
/// 轻量内置“队列”适配（Channel），用于无外部 MQ 时的本地开发。生产可替换为 RabbitMQ 实现同接口。
/// </summary>
public interface IQueuePublisher
{
    Task PublishAsync(string topic, object message, CancellationToken ct = default);
}

public interface IQueueSubscriber
{
    /// <summary>
    /// 订阅主题；回调内处理消息（幂等）。返回一个可取消的任务。
    /// </summary>
    Task SubscribeAsync(string topic, Func<ReadOnlyMemory<byte>, Task> handler, CancellationToken ct = default);
}

public sealed class InMemoryQueue : IQueuePublisher, IQueueSubscriber
{
    private readonly Channel<(string Topic, byte[] Body)> _channel = Channel.CreateUnbounded<(string, byte[])>();
    private readonly Channel<(string Topic, byte[] Body, Exception Error)> _deadLetterChannel = Channel.CreateUnbounded<(string, byte[], Exception)>();
    private readonly QueueMetrics _metrics;

    private const int MaxRetries = 3;

    public InMemoryQueue() : this(new QueueMetrics()) { }

    public InMemoryQueue(QueueMetrics metrics)
    {
        _metrics = metrics;
    }

    /// <summary>
    /// 获取队列度量指标。
    /// </summary>
    public IQueueMetrics Metrics => _metrics;

    /// <summary>
    /// 获取死信读取器（可用于监控/重处理）。
    /// </summary>
    public ChannelReader<(string Topic, byte[] Body, Exception Error)> DeadLetterReader => _deadLetterChannel.Reader;

    public Task PublishAsync(string topic, object message, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        _channel.Writer.TryWrite((topic, body));
        _metrics.IncrementPublished();
        return Task.CompletedTask;
    }

    public async Task SubscribeAsync(string topic, Func<ReadOnlyMemory<byte>, Task> handler, CancellationToken ct = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            if (!string.Equals(item.Topic, topic, StringComparison.Ordinal))
                continue;

            var success = false;
            Exception? lastError = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await handler(item.Body);
                    _metrics.IncrementConsumed();
                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _metrics.IncrementFailed();
                    if (attempt < MaxRetries)
                    {
                        _metrics.IncrementRetried();
                        // 指数退避：100ms, 400ms, 900ms
                        await Task.Delay((attempt + 1) * (attempt + 1) * 100, ct);
                    }
                }
            }

            if (!success && lastError != null)
            {
                // 重试耗尽，写入死信通道
                _deadLetterChannel.Writer.TryWrite((item.Topic, item.Body, lastError));
                _metrics.IncrementDeadLettered();
            }
        }
    }
}


