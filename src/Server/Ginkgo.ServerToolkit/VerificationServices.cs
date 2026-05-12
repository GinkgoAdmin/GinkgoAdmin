using System.Security.Cryptography;
using System.Text;
using Ginkgo.Domain.Verification;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ginkgo.ServerToolkit;

/// <summary>
/// 验证码生成与校验服务（基于 MemoryCache）。
/// </summary>
internal sealed class VerificationCodeService : IVerificationCodeService
{
	private readonly IMemoryCache _cache;
	public VerificationCodeService(IMemoryCache cache) { _cache = cache; }

	public Task<GeneratedCode> GenerateAsync(string purpose, string subject, TimeSpan ttl, int length = 6, bool digitsOnly = true, int? throttleSeconds = 60, CancellationToken ct = default)
	{
		var key = CacheKey(purpose, subject);
		if (throttleSeconds is int t && _cache.TryGetValue(key + ":th", out _))
			throw new InvalidOperationException("TooManyRequests");
		var code = GenerateCode(length, digitsOnly);
		var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
		_cache.Set(key, code, ttl);
		if (throttleSeconds is int th && th > 0) _cache.Set(key + ":th", 1, TimeSpan.FromSeconds(th));
		return Task.FromResult(new GeneratedCode(code, expiresAt));
	}

	public Task<bool> ValidateAsync(string purpose, string subject, string code, bool consumeOnce = true, CancellationToken ct = default)
	{
		var key = CacheKey(purpose, subject);
		if (!_cache.TryGetValue<string>(key, out var saved)) return Task.FromResult(false);
		var ok = string.Equals(saved, code, StringComparison.Ordinal);
		if (ok && consumeOnce) _cache.Remove(key);
		return Task.FromResult(ok);
	}

	private static string CacheKey(string purpose, string subject) => $"{ServerToolkitDefaults.VerificationCacheKeyPrefix}{purpose}:{subject}";

	private static string GenerateCode(int length, bool digitsOnly)
	{
		var rng = Random.Shared;
		if (digitsOnly)
		{
			Span<char> chars = stackalloc char[length];
			for (int i = 0; i < length; i++) chars[i] = (char)('0' + rng.Next(0, 10));
			return new string(chars);
		}
		const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		Span<char> buf = stackalloc char[length];
		for (int i = 0; i < length; i++) buf[i] = alphabet[rng.Next(alphabet.Length)];
		return new string(buf);
	}
}

/// <summary>
/// 二次验证服务完整实现。
/// 提供验证码生成、渠道发送、校验的统一编排。
/// 支持 MemoryCache 快速校验 + 数据库持久化审计。
/// </summary>
internal sealed class SecondaryVerificationService : ISecondaryVerificationService
{
	private readonly IVerificationCodeService _codes;
	private readonly IEmailSender _email;
	private readonly IEnumerable<IVerificationChannelProvider> _channelProviders;
	private readonly IVerificationCodeRepository? _codeRepo;
	private readonly IVerificationTemplateRepository? _templateRepo;
	private readonly IMemoryCache _cache;
	private readonly ILogger<SecondaryVerificationService> _logger;
	private readonly string _appName;

	/// <summary>用途标签映射</summary>
	private static readonly Dictionary<VerificationPurpose, string> PurposeLabels = new()
	{
		[VerificationPurpose.ForgotPassword] = "找回密码",
		[VerificationPurpose.Login] = "登录验证",
		[VerificationPurpose.Register] = "注册验证",
		[VerificationPurpose.BindEmail] = "绑定邮箱",
		[VerificationPurpose.BindPhone] = "绑定手机",
		[VerificationPurpose.DangerousAction] = "操作确认",
		[VerificationPurpose.Custom] = "身份验证",
	};

	public SecondaryVerificationService(
		IVerificationCodeService codes,
		IEmailSender email,
		IEnumerable<IVerificationChannelProvider> channelProviders,
		IMemoryCache cache,
		ILogger<SecondaryVerificationService> logger,
		IVerificationCodeRepository? codeRepo = null,
		IVerificationTemplateRepository? templateRepo = null,
		string? appName = null)
	{
		_codes = codes;
		_email = email;
		_channelProviders = channelProviders;
		_cache = cache;
		_logger = logger;
		_codeRepo = codeRepo;
		_templateRepo = templateRepo;
		_appName = appName ?? "GinkgoAdmin";
	}

	// ===== 可用渠道 =====

	/// <summary>获取当前可用的发送渠道列表</summary>
	public IReadOnlyList<VerificationChannel> AvailableChannels =>
		_channelProviders.Where(p => p.IsAvailable).Select(p => p.Channel).Distinct().ToList().AsReadOnly();

	// ===== 一步式简化 API =====

	/// <summary>生成验证码并通过指定渠道发送</summary>
	public async Task<SendCodeResult> SendVerificationCodeAsync(
		string target,
		VerificationPurpose purpose,
		VerificationChannel channel = VerificationChannel.Email,
		int ttlSeconds = 300,
		int codeLength = 6,
		int throttleSeconds = 60,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(target))
			return SendCodeResult.Fail("接收目标不能为空");

		target = target.Trim().ToLowerInvariant();
		var purposeKey = $"otp:{(int)purpose}";
		var purposeLabel = PurposeLabels.GetValueOrDefault(purpose, "身份验证");

		// 1. 节流检查
		var throttleKey = $"{ServerToolkitDefaults.VerificationCacheKeyPrefix}th:{purposeKey}:{target}";
		if (_cache.TryGetValue<DateTimeOffset>(throttleKey, out var throttleExpiry))
		{
			var remaining = (int)Math.Ceiling((throttleExpiry - DateTimeOffset.UtcNow).TotalSeconds);
			if (remaining > 0)
				return SendCodeResult.Throttled(remaining);
		}

		// 2. 查找渠道提供者
		var provider = _channelProviders.FirstOrDefault(p => p.Channel == channel && p.IsAvailable);
		if (provider == null)
			return SendCodeResult.Fail($"渠道 {channel} 不可用");

		// 3. 生成验证码
		GeneratedCode gen;
		try
		{
			gen = await _codes.GenerateAsync(
				purposeKey, target,
				TimeSpan.FromSeconds(ttlSeconds),
				codeLength, digitsOnly: true,
				throttleSeconds: null, // 节流由本层管理
				ct: ct);
		}
		catch (InvalidOperationException)
		{
			return SendCodeResult.Throttled(throttleSeconds);
		}

		// 4. 查询模板并替换占位符
		string? templateBody = null;
		string? templateSubject = null;
		if (_templateRepo != null)
		{
			try
			{
				var template = await _templateRepo.GetDefaultAsync((int)purpose, (int)channel, ct);
				if (template != null && !string.IsNullOrWhiteSpace(template.BodyTemplate))
				{
					var minutes = Math.Max(1, ttlSeconds / 60);
					templateBody = template.BodyTemplate
						.Replace("{code}", gen.Code)
						.Replace("{minutes}", minutes.ToString())
						.Replace("{purpose}", purposeLabel)
						.Replace("{appName}", _appName);
					templateSubject = template.Subject?
						.Replace("{purpose}", purposeLabel)
						.Replace("{appName}", _appName);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "查询验证码模板失败，将使用内置默认模板");
			}
		}

		// 5. 通过渠道发送
		try
		{
			if (!string.IsNullOrWhiteSpace(templateBody) && channel == VerificationChannel.Email)
			{
				// 使用数据库模板发送邮件
				var msg = new EmailMessage(
					To: target,
					Subject: templateSubject ?? $"{_appName} {purposeLabel} - 验证码",
					Body: templateBody,
					IsHtml: true);
				await _email.SendAsync(msg, ct);
			}
			else
			{
				// 使用渠道提供者发送（内置模板）
				await provider.SendAsync(target, gen.Code, purposeLabel, ttlSeconds, ct);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "验证码发送失败 Channel={Channel} Target={Target} Purpose={Purpose}",
				channel, target, purpose);
			return SendCodeResult.Fail("验证码发送失败，请稍后重试");
		}

		// 6. 设置节流
		var throttleExpireAt = DateTimeOffset.UtcNow.AddSeconds(throttleSeconds);
		_cache.Set(throttleKey, throttleExpireAt, TimeSpan.FromSeconds(throttleSeconds));

		// 7. 写入数据库记录（审计）
		if (_codeRepo != null)
		{
			try
			{
				var record = new VerificationCode
				{
					Target = target,
					Channel = (int)channel,
					Purpose = (int)purpose,
					PurposeLabel = purposeLabel,
					CodeHash = ComputeSha256(gen.Code),
					ExpiresAt = DateTime.Now.AddSeconds(ttlSeconds),
					MaxAttempts = 5,
				};
				await _codeRepo.AddAsync(record, ct);
			}
			catch (Exception ex)
			{
				// 审计写入失败不影响主流程
				_logger.LogWarning(ex, "验证码记录写入数据库失败（不影响发送）");
			}
		}

		_logger.LogInformation("验证码已发送 Channel={Channel} Target={Target} Purpose={Purpose}", channel, target, purpose);
		return SendCodeResult.Ok(throttleSeconds);
	}

	/// <summary>校验用户提交的验证码</summary>
	public async Task<ValidateCodeResult> ValidateVerificationCodeAsync(
		string target,
		VerificationPurpose purpose,
		string code,
		bool consumeOnSuccess = true,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(code))
			return ValidateCodeResult.Fail("参数不能为空");

		target = target.Trim().ToLowerInvariant();
		code = code.Trim();
		var purposeKey = $"otp:{(int)purpose}";

		// 1. 优先从 MemoryCache 校验（快）
		var cacheOk = await _codes.ValidateAsync(purposeKey, target, code, consumeOnSuccess, ct);

		if (cacheOk)
		{
			// 更新数据库记录
			await MarkVerifiedInDbAsync(target, (int)purpose, code, ct);
			_logger.LogInformation("验证码校验通过（Cache） Target={Target} Purpose={Purpose}", target, purpose);
			return ValidateCodeResult.Ok();
		}

		// 2. Cache 未命中时回退到数据库查询（重启恢复场景）
		if (_codeRepo != null)
		{
			try
			{
				var record = await _codeRepo.GetLatestAsync(target, (int)purpose, ct);
				if (record == null)
					return ValidateCodeResult.Fail("验证码不存在或已过期");

				// 检查最大校验次数
				if (record.Attempts >= record.MaxAttempts)
				{
					_logger.LogWarning("验证码校验次数超限 Target={Target} Purpose={Purpose} Attempts={Attempts}",
						target, purpose, record.Attempts);
					return ValidateCodeResult.Fail("校验次数已耗尽，请重新获取验证码");
				}

				// 更新尝试次数
				record.Attempts++;

				var codeHash = ComputeSha256(code);
				if (string.Equals(record.CodeHash, codeHash, StringComparison.Ordinal))
				{
					// 校验通过
					if (consumeOnSuccess)
						record.VerifiedAt = DateTime.Now;
					await _codeRepo.UpdateAsync(record, ct);
					_logger.LogInformation("验证码校验通过（DB回退） Target={Target} Purpose={Purpose}", target, purpose);
					return ValidateCodeResult.Ok();
				}
				else
				{
					// 校验失败，更新尝试次数
					await _codeRepo.UpdateAsync(record, ct);
					var remaining = record.MaxAttempts - record.Attempts;
					return ValidateCodeResult.Fail($"验证码错误，还可尝试 {remaining} 次");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "数据库验证码校验异常");
			}
		}

		return ValidateCodeResult.Fail("验证码错误或已过期");
	}

	// ===== 兼容旧接口实现 =====

	/// <summary>创建验证挑战（兼容旧接口）</summary>
	public async Task<SecondFactorChallenge> CreateChallengeAsync(string purpose, string subject, string channel, TimeSpan? ttl = null, CancellationToken ct = default)
	{
		var life = ttl ?? TimeSpan.FromMinutes(5);
		var gen = await _codes.GenerateAsync($"2fa:{purpose}:{channel}", subject, life, digitsOnly: true, ct: ct);
		return new SecondFactorChallenge(Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(), purpose, subject, channel, gen.ExpiresAt);
	}

	/// <summary>发送验证码（兼容旧接口 — 暂不实现，请使用一步式 API）</summary>
	public Task SendCodeAsync(long challengeId, CancellationToken ct = default)
	{
		_logger.LogWarning("调用了旧版 SendCodeAsync(challengeId={ChallengeId})，请迁移到 SendVerificationCodeAsync", challengeId);
		return Task.CompletedTask;
	}

	/// <summary>校验验证码（兼容旧接口 — 暂不实现，请使用一步式 API）</summary>
	public Task<bool> VerifyAsync(long challengeId, string code, CancellationToken ct = default)
	{
		_logger.LogWarning("调用了旧版 VerifyAsync(challengeId={ChallengeId})，请迁移到 ValidateVerificationCodeAsync", challengeId);
		return Task.FromResult(false);
	}

	// ===== 工具方法 =====

	/// <summary>标记数据库中的验证码已使用</summary>
	private async Task MarkVerifiedInDbAsync(string target, int purpose, string code, CancellationToken ct)
	{
		if (_codeRepo == null) return;
		try
		{
			var record = await _codeRepo.GetLatestAsync(target, purpose, ct);
			if (record != null && string.Equals(record.CodeHash, ComputeSha256(code), StringComparison.Ordinal))
			{
				record.VerifiedAt = DateTime.Now;
				record.Attempts++;
				await _codeRepo.UpdateAsync(record, ct);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "更新验证码数据库记录失败（不影响主流程）");
		}
	}

	/// <summary>计算 SHA256 哈希</summary>
	private static string ComputeSha256(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}
}
