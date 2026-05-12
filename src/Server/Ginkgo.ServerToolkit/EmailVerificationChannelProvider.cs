using Ginkgo.ServerToolkit;
using Microsoft.Extensions.Logging;

namespace Ginkgo.ServerToolkit;

/// <summary>
/// 内置邮件验证码渠道提供者（主框架基础能力，始终可用）。
/// </summary>
internal sealed class EmailVerificationChannelProvider : IVerificationChannelProvider
{
	private readonly IEmailSender _emailSender;
	private readonly ILogger<EmailVerificationChannelProvider> _logger;

	public EmailVerificationChannelProvider(IEmailSender emailSender, ILogger<EmailVerificationChannelProvider> logger)
	{
		_emailSender = emailSender;
		_logger = logger;
	}

	/// <summary>渠道类型：邮件</summary>
	public VerificationChannel Channel => VerificationChannel.Email;

	/// <summary>邮件渠道始终注册可用，实际能否发送取决于 SMTP 配置</summary>
	public bool IsAvailable => true;

	/// <summary>通过邮件发送验证码</summary>
	public async Task SendAsync(string target, string code, string purposeLabel, int ttlSeconds, CancellationToken ct = default)
	{
		var minutes = Math.Max(1, ttlSeconds / 60);
		var htmlBody = BuildEmailTemplate(code, purposeLabel, minutes);
		var msg = new EmailMessage(
			To: target,
			Subject: $"GinkgoAdmin {purposeLabel} - 验证码",
			Body: htmlBody,
			IsHtml: true);

		_logger.LogInformation("发送邮件验证码 To={To} Purpose={Purpose}", target, purposeLabel);
		await _emailSender.SendAsync(msg, ct);
	}

	/// <summary>
	/// 构建精美 HTML 邮件模板（内置兜底模板，当数据库无自定义模板时使用）。
	/// </summary>
	internal static string BuildEmailTemplate(string code, string purpose, int minutes)
	{
		return $"""
		<!DOCTYPE html>
		<html>
		<head>
		<meta charset="utf-8">
		<meta name="viewport" content="width=device-width, initial-scale=1.0">
		</head>
		<body style="margin:0;padding:0;background-color:#f4f7fa;font-family:'Segoe UI','PingFang SC','Microsoft YaHei',sans-serif;">
		<table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f4f7fa;padding:40px 0;">
		<tr>
		<td align="center">
		<table role="presentation" width="520" cellspacing="0" cellpadding="0" style="background:#ffffff;border-radius:12px;box-shadow:0 2px 12px rgba(0,0,0,0.08);overflow:hidden;">
		<!-- 顶部渐变条 -->
		<tr>
		<td style="height:6px;background:linear-gradient(90deg,#667eea 0%,#764ba2 100%);"></td>
		</tr>
		<!-- 内容区 -->
		<tr>
		<td style="padding:40px 48px;">
		<h2 style="margin:0 0 8px 0;font-size:22px;color:#1a1a2e;font-weight:600;">GinkgoAdmin</h2>
		<p style="margin:0 0 24px 0;font-size:14px;color:#6b7280;">您正在进行 <strong style="color:#374151;">{purpose}</strong> 操作</p>
		<div style="background:linear-gradient(135deg,#667eea10,#764ba210);border:1px solid #667eea30;border-radius:10px;padding:24px;text-align:center;margin:0 0 24px 0;">
		<p style="margin:0 0 8px 0;font-size:13px;color:#6b7280;letter-spacing:1px;">您的验证码</p>
		<div style="font-size:36px;font-weight:700;letter-spacing:8px;color:#667eea;font-family:'Courier New',monospace;">{code}</div>
		</div>
		<p style="margin:0 0 6px 0;font-size:13px;color:#9ca3af;">• 验证码将在 <strong style="color:#374151;">{minutes} 分钟</strong>后失效</p>
		<p style="margin:0 0 6px 0;font-size:13px;color:#9ca3af;">• 如非本人操作，请忽略此邮件</p>
		<p style="margin:0;font-size:13px;color:#9ca3af;">• 请勿将验证码告知他人</p>
		</td>
		</tr>
		<!-- 底部 -->
		<tr>
		<td style="padding:16px 48px;background:#f9fafb;border-top:1px solid #f3f4f6;">
		<p style="margin:0;font-size:12px;color:#9ca3af;text-align:center;">此邮件由系统自动发送，请勿直接回复</p>
		</td>
		</tr>
		</table>
		</td>
		</tr>
		</table>
		</body>
		</html>
		""";
	}
}
