using Ginkgo.ServerToolkit;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Email;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email] To={To} Subject={Subject} BodyLength={Len}", message.To, message.Subject, message.Body?.Length ?? 0);
        return Task.CompletedTask;
    }
}

