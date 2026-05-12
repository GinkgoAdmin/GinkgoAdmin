// 文件功能说明：
// 模型验证过滤器，在模型绑定失败或校验失败时返回统一结果。

using Ginkgo.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ginkgo.Api.Filters;

/// <summary>
/// 模型验证过滤器。
/// </summary>
public sealed class ValidateModelAttribute : ActionFilterAttribute
{
    /// <summary>
    /// 在 Action 执行前校验模型有效性。
    /// </summary>
    /// <param name="context">执行上下文。</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var message = string.Join("；", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            context.Result = new JsonResult(Result.Fail(1000, message));
        }
    }
}


