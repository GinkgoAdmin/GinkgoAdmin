using Ginkgo.Domain.Events;
using Ginkgo.Domain.Logs.Events;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Application.Logs.Handlers;

/// <summary>
/// 操作日志追加事件（OpLogAppended）的订阅示例处理器。
/// </summary>
public sealed class OpLogAppendedHandler : IDomainEventHandler<OpLogAppended>
{
    private readonly ILogger<OpLogAppendedHandler> _logger;
    public OpLogAppendedHandler(ILogger<OpLogAppendedHandler> logger) { _logger = logger; }

    public Task HandleAsync(OpLogAppended @event, CancellationToken ct = default)
    {
        _logger.LogInformation("OpLog appended: {Id} at {At}", @event.Id, @event.OccurredOn);
        return Task.CompletedTask;
    }
}

