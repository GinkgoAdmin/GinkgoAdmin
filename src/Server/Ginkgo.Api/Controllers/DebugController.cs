using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ginkgo.Infrastructure.Storage;


namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/debug/config")]
[ApiVersion("1.0")]
public sealed class DebugController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public DebugController(IConfiguration cfg) { _cfg = cfg; }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        // 仅返回 Debug 节中与客户端相关的开关，便于客户端按需开启本地详日志
        var dto = new
        {
            Enable = EqualsIgnoreCase(_cfg["Debug:Enable"], "true"),
            EnableRequestLog = EqualsIgnoreCase(_cfg["Debug:EnableRequestLog"], "true"),
            IncludeHeaders = EqualsIgnoreCase(_cfg["Debug:IncludeHeaders"], "true"),
            IncludeRequestBody = EqualsIgnoreCase(_cfg["Debug:IncludeRequestBody"], "true"),
            IncludeResponseBody = EqualsIgnoreCase(_cfg["Debug:IncludeResponseBody"], "true"),
            MaxBodySize = int.TryParse(_cfg["Debug:MaxBodySize"], out var n) ? n : 2048
        };
        return Ok(dto);
    }

#if DEBUG
    [HttpGet("~/api/v{version:apiVersion}/debug/storage")]
    [AllowAnonymous]
    public ActionResult<object> GetStorageProvider([FromServices] Ginkgo.Infrastructure.Runtime.ISwitcher<IFileStorageProvider> switcher)
    {
        return new { Provider = switcher.Current.GetType().FullName };
    }
#endif

    private static bool EqualsIgnoreCase(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}




