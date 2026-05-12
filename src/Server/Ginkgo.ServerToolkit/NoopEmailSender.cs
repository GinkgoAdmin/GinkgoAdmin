namespace Ginkgo.ServerToolkit;

internal sealed class NoopEmailSender : IEmailSender
{
	public Task SendAsync(EmailMessage message, CancellationToken ct = default)
	{
		// 默认空实现：未配置具体 Provider 时不发送邮件
		return Task.CompletedTask;
	}
}






