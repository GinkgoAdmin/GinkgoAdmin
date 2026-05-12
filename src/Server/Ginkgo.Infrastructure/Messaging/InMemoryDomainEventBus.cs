using Ginkgo.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Ginkgo.Infrastructure.Messaging;

/// <summary>
/// 进程内领域事件总线（简单版）。按注册顺序依次调用订阅者，异常会被抛出。
/// </summary>
public sealed class InMemoryDomainEventBus : IDomainEventPublisher
{
    private readonly IServiceProvider _sp;
    public InMemoryDomainEventBus(IServiceProvider sp) { _sp = sp; }

    public async Task PublishAsync(IDomainEvent @event, CancellationToken ct = default)
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        using var scope = _sp.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);
        foreach (var h in handlers)
        {
            var method = handlerType.GetMethod("HandleAsync");
            if (method != null)
            {
                var task = (Task?)method.Invoke(h, new object?[] { @event, ct });
                if (task != null) await task.ConfigureAwait(false);
            }
        }
    }
}

