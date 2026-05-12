using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Ginkgo.ServerToolkit.Email.SmtpProvider;

public sealed class SmtpOptions
{
	public string Host { get; set; } = "localhost";
	public int Port { get; set; } = 25;
	public bool EnableSsl { get; set; } = false;
	public string? User { get; set; }
	public string? Password { get; set; }
	public string From { get; set; } = "no-reply@example.com";
	public string? FromDisplayName { get; set; }
	public string? AuthType { get; set; }
	public int TimeoutMs { get; set; } = 10000;
}

public sealed class SmtpEmailSender : IEmailSender
{
	private readonly SmtpOptions _options;
	public SmtpEmailSender(SmtpOptions options) { _options = options; }

	public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
	{
		// 构建 MimeMessage
		var mime = new MimeMessage();
		mime.From.Add(CreateFromAddress(message));
		mime.To.Add(MailboxAddress.Parse(message.To));
		if (message.Cc != null)
		{
			foreach (var c in message.Cc)
				mime.Cc.Add(MailboxAddress.Parse(c));
		}
		if (message.Bcc != null)
		{
			foreach (var b in message.Bcc)
				mime.Bcc.Add(MailboxAddress.Parse(b));
		}
		mime.Subject = message.Subject;

		// 设置邮件正文
		var bodyBuilder = new BodyBuilder();
		if (message.IsHtml)
			bodyBuilder.HtmlBody = message.Body;
		else
			bodyBuilder.TextBody = message.Body;
		mime.Body = bodyBuilder.ToMessageBody();

		// 使用 MailKit SmtpClient 发送
		using var client = new SmtpClient();
		client.Timeout = _options.TimeoutMs;

		// 根据端口号自动选择安全连接模式：
		// 465 端口使用隐式 SSL/TLS (SslOnConnect)
		// 587 端口使用 STARTTLS 显式升级
		// 25 端口不加密（或根据 EnableSsl 自动选择 STARTTLS）
		var secureSocketOptions = DetermineSecureSocketOptions();
		await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, ct);

		// 认证
		if (ShouldUseCredentials())
		{
			// 移除腾讯企业邮等服务商经常支持不好或容易报错的高级认证方式，强制使用标准 LOGIN/PLAIN
			client.AuthenticationMechanisms.Remove("XOAUTH2");
			client.AuthenticationMechanisms.Remove("NTLM");
			client.AuthenticationMechanisms.Remove("CRAM-MD5");
			await client.AuthenticateAsync(_options.User!, _options.Password!, ct);
		}

		await client.SendAsync(mime, ct);
		await client.DisconnectAsync(true, ct);
	}

	/// <summary>
	/// 根据端口与 EnableSsl 配置推断最合适的安全连接模式。
	/// </summary>
	private SecureSocketOptions DetermineSecureSocketOptions()
	{
		// 465 端口 = 隐式 SSL/TLS
		if (_options.Port == 465)
			return SecureSocketOptions.SslOnConnect;

		// 587 端口 = STARTTLS 显式升级
		if (_options.Port == 587)
			return SecureSocketOptions.StartTls;

		// 其他端口看 EnableSsl 开关
		if (_options.EnableSsl)
			return SecureSocketOptions.StartTlsWhenAvailable;

		// 默认：有就升级，没有也不报错
		return SecureSocketOptions.StartTlsWhenAvailable;
	}

	private bool ShouldUseCredentials()
	{
		if (string.Equals(_options.AuthType, "None", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return !string.IsNullOrWhiteSpace(_options.User);
	}

	private MailboxAddress CreateFromAddress(EmailMessage message)
	{
		if (!string.IsNullOrWhiteSpace(message.From))
		{
			return MailboxAddress.Parse(message.From!);
		}

		if (string.IsNullOrWhiteSpace(_options.FromDisplayName))
		{
			return MailboxAddress.Parse(_options.From);
		}

		return new MailboxAddress(_options.FromDisplayName, _options.From);
	}
}

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddEmailSmtp(this IServiceCollection services, IConfiguration configuration)
	{
		var section = configuration.GetSection($"{ServerToolkitDefaults.ConfigRoot}:Email:Smtp");
		var options = section.Get<SmtpOptions>() ?? new SmtpOptions();
		services.AddSingleton(options);
		services.AddScoped<IEmailSender, SmtpEmailSender>();
		return services;
	}
}
