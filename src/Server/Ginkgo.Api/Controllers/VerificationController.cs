// 文件功能说明：
// 提供验证码发送、校验、渠道查询和模板管理的 REST API。
// 属于主框架基础能力，不依赖任何插件。

using Ginkgo.Domain.Verification;
using Ginkgo.ServerToolkit;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 验证码接口（主框架基础能力）。
/// 提供验证码发送、校验、渠道查询和模板管理。
/// </summary>
[ApiController]
[Route("api/auth/verification")]
public sealed class VerificationController : ControllerBase
{
	private readonly ISecondaryVerificationService _verificationService;
	private readonly IVerificationTemplateRepository? _templateRepo;

	public VerificationController(
		ISecondaryVerificationService verificationService,
		IVerificationTemplateRepository? templateRepo = null)
	{
		_verificationService = verificationService;
		_templateRepo = templateRepo;
	}

	// ===== 公开接口（AllowAnonymous） =====

	/// <summary>
	/// 发送验证码。
	/// </summary>
	[HttpPost("send")]
	[AllowAnonymous]
	[EnableRateLimiting("login")]
	public async Task<Result<SendCodeResult>> Send([FromBody] SendCodeInput input, CancellationToken ct)
	{
		var result = await _verificationService.SendVerificationCodeAsync(
			target: input.Target,
			purpose: input.Purpose,
			channel: input.Channel,
			ttlSeconds: input.TtlSeconds,
			codeLength: input.CodeLength,
			throttleSeconds: input.ThrottleSeconds,
			ct: ct);

		if (!result.Success)
			return Result<SendCodeResult>.Fail(ServerToolkitErrors.TooManyRequests, result.Message);

		return Result<SendCodeResult>.Success(result);
	}

	/// <summary>
	/// 校验验证码。
	/// </summary>
	[HttpPost("validate")]
	[AllowAnonymous]
	[EnableRateLimiting("login")]
	public async Task<Result<ValidateCodeResult>> Validate([FromBody] ValidateCodeInput input, CancellationToken ct)
	{
		var result = await _verificationService.ValidateVerificationCodeAsync(
			target: input.Target,
			purpose: input.Purpose,
			code: input.Code,
			consumeOnSuccess: input.ConsumeOnSuccess,
			ct: ct);

		if (!result.Success)
			return Result<ValidateCodeResult>.Fail(ServerToolkitErrors.VerificationCodeInvalid, result.Message);

		return Result<ValidateCodeResult>.Success(result);
	}

	/// <summary>
	/// 获取当前可用的验证码发送渠道。
	/// </summary>
	[HttpGet("channels")]
	[AllowAnonymous]
	public Result<object> GetChannels()
	{
		var channels = _verificationService.AvailableChannels
			.Select(c => new { value = (int)c, label = GetChannelLabel(c) })
			.ToList();
		return Result<object>.Success(channels);
	}

	// ===== 管理接口（需要鉴权） =====

	/// <summary>
	/// 获取验证码模板列表（管理用）。
	/// </summary>
	[HttpGet("templates")]
	[Authorize]
	public async Task<Result<object>> GetTemplates(CancellationToken ct)
	{
		if (_templateRepo == null)
			return Result<object>.Fail(500, "模板仓储未注册");

		var templates = await _templateRepo.GetAllAsync(ct);
		return Result<object>.Success(templates);
	}

	/// <summary>
	/// 新增或更新验证码模板。
	/// </summary>
	[HttpPost("templates")]
	[Authorize]
	public async Task<Result> SaveTemplate([FromBody] SaveTemplateInput input, CancellationToken ct)
	{
		if (_templateRepo == null)
			return Result.Fail(500, "模板仓储未注册");

		if (input.Id.HasValue && input.Id.Value > 0)
		{
			// 更新
			var existing = await _templateRepo.GetByIdAsync(input.Id.Value, ct);
			if (existing == null)
				return Result.Fail(404, "模板不存在");

			existing.Purpose = (int)input.Purpose;
			existing.Channel = (int)input.Channel;
			existing.Name = input.Name;
			existing.Subject = input.Subject;
			existing.BodyTemplate = input.BodyTemplate;
			existing.IsHtml = input.IsHtml;
			existing.IsDefault = input.IsDefault;
			existing.Enabled = input.Enabled;
			existing.SortOrder = input.SortOrder;

			// 如果设为默认，先取消同 Purpose+Channel 组中其他模板的默认标记
			if (input.IsDefault)
				await _templateRepo.ClearDefaultAsync((int)input.Purpose, (int)input.Channel, existing.Id, ct);

			await _templateRepo.UpdateAsync(existing, ct);
		}
		else
		{
			// 新增
			var template = new VerificationTemplate
			{
				Purpose = (int)input.Purpose,
				Channel = (int)input.Channel,
				Name = input.Name,
				Subject = input.Subject,
				BodyTemplate = input.BodyTemplate,
				IsHtml = input.IsHtml,
				IsDefault = input.IsDefault,
				Enabled = input.Enabled,
				SortOrder = input.SortOrder,
			};

			// 如果设为默认，先取消同 Purpose+Channel 组中其他模板的默认标记
			if (input.IsDefault)
				await _templateRepo.ClearDefaultAsync((int)input.Purpose, (int)input.Channel, 0, ct);

			await _templateRepo.AddAsync(template, ct);
		}

		return Result.Success("保存成功");
	}

	/// <summary>
	/// 删除验证码模板。
	/// </summary>
	[HttpDelete("templates/{id}")]
	[Authorize]
	public async Task<Result> DeleteTemplate(long id, CancellationToken ct)
	{
		if (_templateRepo == null)
			return Result.Fail(500, "模板仓储未注册");

		await _templateRepo.DeleteAsync(id, ct);
		return Result.Success("删除成功");
	}

	// ===== 辅助方法 =====

	/// <summary>获取渠道中文标签</summary>
	private static string GetChannelLabel(VerificationChannel channel) => channel switch
	{
		VerificationChannel.Email => "邮箱",
		VerificationChannel.Sms => "短信",
		VerificationChannel.InApp => "站内通知",
		_ => channel.ToString()
	};
}

// ===== 请求 DTO =====

/// <summary>
/// 发送验证码请求。
/// </summary>
public sealed class SendCodeInput
{
	/// <summary>接收目标（邮箱地址或手机号）</summary>
	[Required(ErrorMessage = "接收目标不能为空")]
	public string Target { get; set; } = string.Empty;

	/// <summary>验证用途</summary>
	public VerificationPurpose Purpose { get; set; } = VerificationPurpose.DangerousAction;

	/// <summary>发送渠道</summary>
	public VerificationChannel Channel { get; set; } = VerificationChannel.Email;

	/// <summary>有效期（秒），默认 300</summary>
	public int TtlSeconds { get; set; } = 300;

	/// <summary>验证码位数，默认 6</summary>
	public int CodeLength { get; set; } = 6;

	/// <summary>最小发送间隔（秒），默认 60</summary>
	public int ThrottleSeconds { get; set; } = 60;
}

/// <summary>
/// 校验验证码请求。
/// </summary>
public sealed class ValidateCodeInput
{
	/// <summary>接收目标（必须与发送时一致）</summary>
	[Required(ErrorMessage = "接收目标不能为空")]
	public string Target { get; set; } = string.Empty;

	/// <summary>验证用途（必须与发送时一致）</summary>
	public VerificationPurpose Purpose { get; set; }

	/// <summary>用户输入的验证码</summary>
	[Required(ErrorMessage = "验证码不能为空")]
	public string Code { get; set; } = string.Empty;

	/// <summary>校验通过后是否消费（一次性使用），默认 true</summary>
	public bool ConsumeOnSuccess { get; set; } = true;
}

/// <summary>
/// 保存模板请求。
/// </summary>
public sealed class SaveTemplateInput
{
	/// <summary>模板ID（有值则更新，无值则新增）</summary>
	public long? Id { get; set; }

	/// <summary>验证用途</summary>
	public VerificationPurpose Purpose { get; set; }

	/// <summary>渠道</summary>
	public VerificationChannel Channel { get; set; }

	/// <summary>模板名称</summary>
	[Required(ErrorMessage = "模板名称不能为空")]
	public string Name { get; set; } = string.Empty;

	/// <summary>邮件主题</summary>
	public string? Subject { get; set; }

	/// <summary>模板正文</summary>
	[Required(ErrorMessage = "模板正文不能为空")]
	public string BodyTemplate { get; set; } = string.Empty;

	/// <summary>是否HTML格式</summary>
	public bool IsHtml { get; set; } = true;

	/// <summary>是否默认模板</summary>
	public bool IsDefault { get; set; }

	/// <summary>是否启用</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>排序</summary>
	public int SortOrder { get; set; }
}
