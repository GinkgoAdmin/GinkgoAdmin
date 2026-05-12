using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Security.Claims;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// Web 站点用户聚合接口。
/// </summary>
[ApiController]
[Route("api/web/user")]
[AllowAnonymous]
public sealed class WebUserController : ControllerBase
{
	private readonly ISqlSugarClient _sugar;

	public WebUserController(ISqlSugarClient sugar)
	{
		_sugar = sugar;
	}

	private static bool TryGetCurrentUserId(ClaimsPrincipal? principal, out long userId)
	{
		userId = default;
		if (principal == null) return false;
		var claim = principal.FindFirst(ClaimTypes.NameIdentifier)
					?? principal.FindFirst(c => string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase))
					?? principal.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase) || c.Type.EndsWith("/sub", StringComparison.OrdinalIgnoreCase));
		if (claim == null || string.IsNullOrWhiteSpace(claim.Value)) return false;
		return long.TryParse(claim.Value, out userId);
	}

	/// <summary>
	/// 个人中心首页聚合数据（当前登录用户）。
	/// </summary>
	[HttpGet("center")]
	public async Task<IActionResult> GetCenterAsync()
	{
		if (!TryGetCurrentUserId(User, out var userId)) return Unauthorized(new { message = "未登录" });

		// 操作日志统计（真实表 ginkgo_Sys_OpLog）
		var q = _sugar.Queryable<Ginkgo.Domain.Logs.OpLog>().Where(x => x.CreatedBy == userId);
		var totalOps = await q.CountAsync();

		var loginLogs = await _sugar.Queryable<Ginkgo.Domain.Logs.OpLog>()
			.Where(x => x.CreatedBy == userId && x.Resource == "/api/auth/login")
			.Select(x => new { x.CreatedAt })
			.ToListAsync();
		var loginDays = loginLogs.Select(x => x.CreatedAt.Date).Distinct().Count();
		DateTime? lastLogin = loginLogs.Count > 0 ? loginLogs.Max(x => x.CreatedAt) : (DateTime?)null;

		var recent = await q.OrderBy(x => x.CreatedAt, OrderByType.Desc)
			.Take(10)
			.Select(x => new
			{
				id = x.Id,
				action = x.Action,
				resource = x.Resource,
				module = x.ModuleCN,
				feature = x.FeatureCN,
				review = x.ReviewCN,
				createdAt = x.CreatedAt
			})
			.ToListAsync();

		return Ok(new
		{
			loginDays,
			lastLoginTime = lastLogin,
			operationCount = totalOps,
			recentActivities = recent
		});
	}
}
