namespace Ginkgo.ServerToolkit;

/// <summary>
/// 抽象：当前登录用户访问。
/// </summary>
public interface ICurrentUser
{
	long? Id { get; }
	string? UserName { get; }
	string? DisplayName { get; }
	IReadOnlyList<string> Roles { get; }
	bool IsAuthenticated { get; }
	long GetIdOrThrow();
}

/// <summary>
/// 抽象：系统通知受众规范。
/// </summary>
public sealed record NotifyAudienceSpec(
	IReadOnlyList<long>? UserIds = null,
	IReadOnlyList<long>? RoleIds = null,
	IReadOnlyList<(long DeptId, bool Deep)>? Departments = null,
	bool ToAll = false
);

/// <summary>
/// 抽象：系统通知发送聚合（适配现有通知模块）。
/// </summary>
public interface IServerNotifier
{
	Task<long> SendAsync(
		string title,
		string content,
		NotifyAudienceSpec audience,
		bool html = false,
		bool important = false,
		byte priority = 1,
		string? dedupeKey = null,
		CancellationToken ct = default);
}

/// <summary>
/// 抽象：邮件消息。
/// </summary>
public sealed record EmailMessage(
	string To,
	string Subject,
	string Body,
	bool IsHtml = true,
	string? From = null,
	IReadOnlyList<string>? Cc = null,
	IReadOnlyList<string>? Bcc = null
);

/// <summary>
/// 抽象：邮件发送。
/// </summary>
public interface IEmailSender
{
	Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// 抽象：生成的验证码。
/// </summary>
public sealed record GeneratedCode(string Code, DateTimeOffset ExpiresAt);

/// <summary>
/// 抽象：验证码服务。
/// </summary>
public interface IVerificationCodeService
{
	Task<GeneratedCode> GenerateAsync(
		string purpose,
		string subject,
		TimeSpan ttl,
		int length = 6,
		bool digitsOnly = true,
		int? throttleSeconds = 60,
		CancellationToken ct = default);

	Task<bool> ValidateAsync(
		string purpose,
		string subject,
		string code,
		bool consumeOnce = true,
		CancellationToken ct = default);
}

/// <summary>
/// 验证码发送渠道标识。
/// </summary>
public enum VerificationChannel
{
	/// <summary>邮件</summary>
	Email = 0,
	/// <summary>短信（需安装 SMS 插件）</summary>
	Sms = 1,
	/// <summary>站内通知</summary>
	InApp = 2
}

/// <summary>
/// 验证码用途标识（用于隔离不同场景的验证码）。
/// </summary>
public enum VerificationPurpose
{
	/// <summary>找回密码</summary>
	ForgotPassword = 0,
	/// <summary>登录验证</summary>
	Login = 1,
	/// <summary>注册验证</summary>
	Register = 2,
	/// <summary>绑定邮箱</summary>
	BindEmail = 3,
	/// <summary>绑定手机</summary>
	BindPhone = 4,
	/// <summary>危险操作确认</summary>
	DangerousAction = 10,
	/// <summary>自定义</summary>
	Custom = 99
}

/// <summary>
/// 抽象：验证码渠道提供者。
/// 主框架内置邮件渠道，SMS 插件可注册短信渠道。
/// </summary>
public interface IVerificationChannelProvider
{
	/// <summary>支持的渠道类型</summary>
	VerificationChannel Channel { get; }

	/// <summary>当前渠道是否可用（已配置且有效）</summary>
	bool IsAvailable { get; }

	/// <summary>
	/// 发送验证码。
	/// </summary>
	/// <param name="target">接收目标（邮箱地址或手机号）</param>
	/// <param name="code">验证码内容</param>
	/// <param name="purposeLabel">用途描述（用于邮件/短信正文）</param>
	/// <param name="ttlSeconds">有效期（秒）</param>
	/// <param name="ct">取消令牌</param>
	Task SendAsync(string target, string code, string purposeLabel, int ttlSeconds, CancellationToken ct = default);
}

/// <summary>
/// 验证码发送结果。
/// </summary>
public sealed class SendCodeResult
{
	/// <summary>是否发送成功</summary>
	public bool Success { get; set; }
	/// <summary>提示消息</summary>
	public string Message { get; set; } = string.Empty;
	/// <summary>距离可重新发送的剩余秒数（前端倒计时用）</summary>
	public int CooldownSeconds { get; set; }

	/// <summary>创建成功结果</summary>
	public static SendCodeResult Ok(int cooldown = 60) => new() { Success = true, Message = "验证码已发送", CooldownSeconds = cooldown };
	/// <summary>创建失败结果</summary>
	public static SendCodeResult Fail(string msg) => new() { Success = false, Message = msg };
	/// <summary>创建节流结果</summary>
	public static SendCodeResult Throttled(int remaining) => new() { Success = false, Message = $"请 {remaining} 秒后再试", CooldownSeconds = remaining };
}

/// <summary>
/// 验证码校验结果。
/// </summary>
public sealed class ValidateCodeResult
{
	/// <summary>是否校验通过</summary>
	public bool Success { get; set; }
	/// <summary>提示消息</summary>
	public string Message { get; set; } = string.Empty;
	/// <summary>验证通过后的一次性凭证（可选，供后续 API 二次确认）</summary>
	public string? VerifiedToken { get; set; }

	/// <summary>创建成功结果</summary>
	public static ValidateCodeResult Ok(string? token = null) => new() { Success = true, Message = "验证通过", VerifiedToken = token };
	/// <summary>创建失败结果</summary>
	public static ValidateCodeResult Fail(string msg = "验证码错误") => new() { Success = false, Message = msg };
}

/// <summary>
/// 抽象：二次验证挑战。
/// </summary>
public sealed record SecondFactorChallenge(
	long ChallengeId,
	string Purpose,
	string Subject,
	string Channel,
	DateTimeOffset ExpiresAt
);

/// <summary>
/// 抽象：二次验证服务。
/// 提供验证码生成、发送、校验的统一编排。
/// </summary>
public interface ISecondaryVerificationService
{
	// ===== 兼容旧接口 =====

	/// <summary>创建验证挑战（兼容旧接口）</summary>
	Task<SecondFactorChallenge> CreateChallengeAsync(
		string purpose,
		string subject,
		string channel,
		TimeSpan? ttl = null,
		CancellationToken ct = default);

	/// <summary>发送验证码（兼容旧接口）</summary>
	Task SendCodeAsync(long challengeId, CancellationToken ct = default);

	/// <summary>校验验证码（兼容旧接口）</summary>
	Task<bool> VerifyAsync(long challengeId, string code, CancellationToken ct = default);

	// ===== 一步式简化 API =====

	/// <summary>
	/// 生成验证码并通过指定渠道发送。一步完成。
	/// </summary>
	/// <param name="target">接收目标（邮箱地址或手机号）</param>
	/// <param name="purpose">验证用途</param>
	/// <param name="channel">发送渠道，默认邮件</param>
	/// <param name="ttlSeconds">有效期（秒），默认 300</param>
	/// <param name="codeLength">验证码位数，默认 6</param>
	/// <param name="throttleSeconds">最小发送间隔（秒），默认 60</param>
	/// <param name="ct">取消令牌</param>
	Task<SendCodeResult> SendVerificationCodeAsync(
		string target,
		VerificationPurpose purpose,
		VerificationChannel channel = VerificationChannel.Email,
		int ttlSeconds = 300,
		int codeLength = 6,
		int throttleSeconds = 60,
		CancellationToken ct = default);

	/// <summary>
	/// 校验用户提交的验证码。
	/// </summary>
	/// <param name="target">接收目标（必须与发送时一致）</param>
	/// <param name="purpose">验证用途（必须与发送时一致）</param>
	/// <param name="code">用户输入的验证码</param>
	/// <param name="consumeOnSuccess">校验通过后是否消费（一次性使用），默认 true</param>
	/// <param name="ct">取消令牌</param>
	Task<ValidateCodeResult> ValidateVerificationCodeAsync(
		string target,
		VerificationPurpose purpose,
		string code,
		bool consumeOnSuccess = true,
		CancellationToken ct = default);

	/// <summary>
	/// 获取当前可用的发送渠道列表。
	/// </summary>
	IReadOnlyList<VerificationChannel> AvailableChannels { get; }
}

/// <summary>
/// 门面：统一聚合常用能力供模块注入使用。
/// </summary>
public interface IServerToolkit
{
	ICurrentUser CurrentUser { get; }
	IServerNotifier Notifier { get; }
	IEmailSender EmailSender { get; }
	IVerificationCodeService VerificationCodes { get; }
	ISecondaryVerificationService SecondFactor { get; }
}

/// <summary>
/// 约定：统一错误码常量。
/// </summary>
public static class ServerToolkitErrors
{
	public const int Unauthorized = 401;
	public const int Forbidden = 403;
	public const int SecondFactorRequired = 428;
	public const int TooManyRequests = 429;
	public const int VerificationCodeExpired = 440;
	public const int VerificationCodeInvalid = 441;
	public const int EmailSendFailed = 502;
}

/// <summary>
/// 约定：默认配置键与标准头/字段名。
/// </summary>
public static class ServerToolkitDefaults
{
	public const string ConfigRoot = "ServerToolkit";
	public const string SecondFactorHeaderName = "X-Second-Factor-Code";
	public const string SecondFactorBodyField = "secondFactorCode";
	public const string VerificationCacheKeyPrefix = "vcode:";
}
