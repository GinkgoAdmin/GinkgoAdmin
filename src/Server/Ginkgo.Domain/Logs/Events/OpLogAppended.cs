using Ginkgo.Domain.Events;

namespace Ginkgo.Domain.Logs.Events;

/// <summary>
/// 操作日志已追加（OpLog appended）事件。
/// </summary>
public sealed class OpLogAppended : IDomainEvent
{
    public OpLogAppended(long id)
    {
        Id = id;
        OccurredOn = DateTime.Now;
    }
    public long Id { get; }
    public DateTime OccurredOn { get; }
}

