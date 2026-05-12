namespace Ginkgo.Domain.Events;

/// <summary>
/// 领域事件发布器。
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(IDomainEvent @event, CancellationToken ct = default);
}

