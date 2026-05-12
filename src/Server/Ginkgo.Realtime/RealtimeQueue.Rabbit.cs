using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ginkgo.Realtime;

/// <summary>
/// 基于 RabbitMQ 的队列实现，支持完整连接配置、自动连接恢复和持久化队列。
/// </summary>
public sealed class RabbitQueue : IQueuePublisher, IQueueSubscriber, IDisposable
{
    private readonly IConnection _conn;
    private readonly IModel _ch;
    private readonly string _exchange;
    private readonly object _publishLock = new();

    public RabbitQueue(
        string host = "localhost",
        int port = 5672,
        string userName = "guest",
        string password = "guest",
        string virtualHost = "/",
        string exchange = "ginkgo.bus")
    {
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            DispatchConsumersAsync = true,
            // 自动连接恢复：断线后自动重连
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            // 自动恢复拓扑（Exchange / Queue / Binding）
            TopologyRecoveryEnabled = true,
        };

        _conn = factory.CreateConnection();
        _ch = _conn.CreateModel();
        _exchange = exchange;
        _ch.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: true);
    }

    public Task PublishAsync(string topic, object message, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        // IModel 非线程安全，通过锁保护并发发布
        lock (_publishLock)
        {
            var props = _ch.CreateBasicProperties();
            props.Persistent = true;
            _ch.BasicPublish(_exchange, topic, props, body);
        }
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string topic, Func<ReadOnlyMemory<byte>, Task> handler, CancellationToken ct = default)
    {
        // 使用持久化、命名队列，防止服务重启后队列丢失
        var queueName = $"ginkgo.{topic}";
        _ch.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        _ch.QueueBind(queueName, _exchange, routingKey: topic);
        // 预取限制：每次只取 1 条，处理完再拉取下一条
        _ch.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                await handler(ea.Body);
                _ch.BasicAck(ea.DeliveryTag, false);
            }
            catch
            {
                // 处理失败则重新入队
                _ch.BasicNack(ea.DeliveryTag, false, true);
            }
        };
        _ch.BasicConsume(queueName, autoAck: false, consumer);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _ch?.Dispose(); } catch { /* 清理：忽略关闭异常 */ }
        try { _conn?.Dispose(); } catch { /* 清理：忽略关闭异常 */ }
    }
}


