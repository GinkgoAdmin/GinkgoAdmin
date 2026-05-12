namespace Ginkgo.Domain.Events;

/// <summary>
/// 领域事件基接口。
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

