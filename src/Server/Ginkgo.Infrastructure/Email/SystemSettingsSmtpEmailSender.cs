using Ginkgo.Domain.Settings;
using Ginkgo.ServerToolkit;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Email;

/// <summary>
/// 系统邮件配置键。
/// </summary>
internal static class MailSettingKeys
{
    public const string SmtpHost = "Mail.Smtp.Host";
    public const string SmtpPort = "Mail.Smtp.Port";
    public const string SslEnable = "Mail.Ssl.Enable";
    public const string SmtpUserName = "Mail.Smtp.UserName";
    public const string SmtpPassword = "Mail.Smtp.Password";
    public const string SmtpAuthType = "Mail.Smtp.AuthType";
    public const string FromAddress = "Mail.From.Address";
    public const string FromDisplayName = "Mail.From.DisplayName";
}

/// <summary>
/// 把系统设置表中的 Mail.* 键映射为 SMTP 发送选项。
/// </summary>
public static class MailSmtpOptionsResolver
{
    public static bool TryResolve(IEnumerable<Setting> settings, out SmtpOptions? options, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var map = settings
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        return TryResolve(map, out options, out reason);
    }

    public static bool TryResolve(IReadOnlyDictionary<string, string?> settings, out SmtpOptions? options, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(settings);

        options = null;

        var host = GetTrimmed(settings, MailSettingKeys.SmtpHost);
        if (string.IsNullOrWhiteSpace(host))
        {
            reason = "未配置 SMTP 服务器地址";
            return false;
        }

        var userName = GetTrimmed(settings, MailSettingKeys.SmtpUserName);
        var fromAddress = GetTrimmed(settings, MailSettingKeys.FromAddress);
        if (string.IsNullOrWhiteSpace(fromAddress) && !string.IsNullOrWhiteSpace(userName) && userName.Contains('@'))
        {
            fromAddress = userName;
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            reason = "未配置发件人邮箱";
            return false;
        }

        var port = 25;
        var portText = GetTrimmed(settings, MailSettingKeys.SmtpPort);
        if (!string.IsNullOrWhiteSpace(portText) && (!int.TryParse(portText, out port) || port <= 0))
        {
            reason = "SMTP 端口格式不正确";
            return false;
        }

        options = new SmtpOptions
        {
            Host = host,
            Port = port,
            EnableSsl = ParseBool(settings, MailSettingKeys.SslEnable),
            User = userName,
            Password = GetTrimmed(settings, MailSettingKeys.SmtpPassword),
            AuthType = NormalizeAuthType(GetTrimmed(settings, MailSettingKeys.SmtpAuthType), userName),
            From = fromAddress,
            FromDisplayName = GetTrimmed(settings, MailSettingKeys.FromDisplayName)
        };

        reason = null;
        return true;
    }

    private static string? GetTrimmed(IReadOnlyDictionary<string, string?> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string?> settings, string key)
    {
        var value = GetTrimmed(settings, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAuthType(string? authType, string? userName)
    {
        if (string.IsNullOrWhiteSpace(authType))
        {
            return string.IsNullOrWhiteSpace(userName) ? "None" : "Login";
        }

        if (authType.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return "None";
        }

        if (authType.Equals("plain", StringComparison.OrdinalIgnoreCase))
        {
            return "Plain";
        }

        if (authType.Equals("cram-md5", StringComparison.OrdinalIgnoreCase) || authType.Equals("crammd5", StringComparison.OrdinalIgnoreCase))
        {
            return "CramMd5";
        }

        return "Login";
    }
}

/// <summary>
/// 基于系统设置表读取 SMTP 配置并执行真实发信。
/// </summary>
public sealed class SystemSettingsSmtpEmailSender : IEmailSender
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<SystemSettingsSmtpEmailSender> _logger;

    public SystemSettingsSmtpEmailSender(
        ISettingsRepository settingsRepository,
        ILogger<SystemSettingsSmtpEmailSender> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var settings = await _settingsRepository.GetAllAsync(ct);
        if (!MailSmtpOptionsResolver.TryResolve(settings, out var options, out var reason))
        {
            _logger.LogWarning("邮件发送已跳过：{Reason}。To={To} Subject={Subject}", reason, message.To, message.Subject);
            return;
        }

        try
        {
            var sender = new SmtpEmailSender(options!);
            await sender.SendAsync(message, ct);
            _logger.LogInformation("邮件发送成功 To={To} Subject={Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败 To={To} Subject={Subject}", message.To, message.Subject);
            throw;
        }
    }
}
