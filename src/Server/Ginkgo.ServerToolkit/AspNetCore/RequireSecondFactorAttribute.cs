using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ginkgo.ServerToolkit.AspNetCore;

/// <summary>
/// 提交时二次验证占位过滤器：当前仅返回 428，提示客户端发起二次验证流程。
/// 生产实现应结合挑战存储与 ISecondaryVerificationService。
/// </summary>
public sealed class RequireSecondFactorAttribute : ActionFilterAttribute
{
	public override void OnActionExecuting(ActionExecutingContext context)
	{
		// 占位：直接要求前端完成二次验证。
		context.Result = new ObjectResult(new { code = ServerToolkitErrors.SecondFactorRequired, message = "SECOND_FACTOR_REQUIRED" })
		{
			StatusCode = ServerToolkitErrors.SecondFactorRequired
		};
	}
}
