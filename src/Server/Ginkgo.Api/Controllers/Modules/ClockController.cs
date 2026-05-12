using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Ginkgo.Api.Controllers.Modules;

/// <summary>
/// 示例模块：系统时钟（演示权限接入与中间件提示）。
/// </summary>
[ApiController]
[Route("api/modules/clock")] // 建议安装 SQL 中将路由绑定到菜单与权限码 clock.view
[Authorize(Policy = "Permission")]
public sealed class ClockController : ControllerBase
{
    /// <summary>
    /// 需要权限：clock.view（示例）。
    /// </summary>
    [HttpGet("now")] // GET /api/modules/clock/now
    public ActionResult<object> Now()
    {
        // 返回服务器时间（UTC 与本地）
        var utc = DateTime.Now;
        var local = utc.ToLocalTime();
        return Ok(new { utc, local });
    }
}


