using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ginkgo.Domain;
using Ginkgo.Application.Logs; // 引入应用层日志服务
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/logs")]
[ApiVersion("1.0")]
[Authorize]
public sealed class LogsController : ControllerBase
{
    // 集成应用层服务，后续可渐进替换控制器内部实现为调用应用服务
    private readonly ILogAppService _logService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogAppService logService, ILogger<LogsController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// 获取全部操作日志，支持按用户过滤。
    /// </summary>
    /// <param name="page">页码（从1开始）。</param>
    /// <param name="pageSize">每页数量。</param>
    /// <param name="userId">可选，按用户筛选（Snowflake ID）。</param>
    /// <param name="filter">可选的过滤条件。</param>
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] long? userId = null, [FromQuery] string? filter = null)
    {
        // 兼容原有分页参数校验
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        try
        {
            var filterInput = ParseFilter(filter);

            // 通过应用层服务获取分页结果（包含用户名显示信息）
            var result = await _logService.GetPagedAsync(new ListOpLogsInput
            {
                Page = page,
                PageSize = pageSize,
                UserId = userId,
                Module = filterInput.Module,
                Feature = filterInput.Feature,
                Type = filterInput.Type,
                Keyword = filterInput.Keyword,
                From = filterInput.From,
                To = filterInput.To
            }, HttpContext.RequestAborted);

            // 保持与原有响应 JSON 结构一致：total/page/pageSize/items
            var items = result.Items.Select(x => new
            {
                x.Id,
                x.UserId,
                x.Action,
                x.Resource,
                x.Ip,
                x.UserAgent,
                x.Result,
                x.ElapsedMs,
                x.DataJson,
                x.ModuleCN,
                x.FeatureCN,
                x.ReviewCN,
                x.CreatedAt,
                x.UserName,
                x.DisplayName,
                x.Email,
                x.Phone
            }).ToList();
            return Ok(new { total = result.Total, page = result.Page, pageSize = result.PageSize, items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取操作日志失败: page={Page}, pageSize={PageSize}, userId={UserId}", page, pageSize, userId);
            return StatusCode(500, new { message = "服务器内部错误" });
        }
    }

    [HttpGet("my")]
    [AllowAnonymous]
    public async Task<ActionResult> GetMy([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? filter = null)
    {
        var uid = HttpContext.User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (!long.TryParse(uid, out var userId)) return Unauthorized(new { message = "未登录" });
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        try
        {
            var filterInput = ParseFilter(filter);

            var result = await _logService.GetPagedAsync(new ListOpLogsInput
            {
                Page = page,
                PageSize = pageSize,
                UserId = userId,
                Module = filterInput.Module,
                Feature = filterInput.Feature,
                Type = filterInput.Type,
                Keyword = filterInput.Keyword,
                From = filterInput.From,
                To = filterInput.To
            }, HttpContext.RequestAborted);

            // “我的日志”保持与原实现一致：items 为实体字段集合（不包含用户名展示字段）
            var items = result.Items.Select(x => new
            {
                x.Id,
                x.UserId,
                x.Action,
                x.Resource,
                x.Ip,
                x.UserAgent,
                x.Result,
                x.ElapsedMs,
                x.DataJson,
                x.ModuleCN,
                x.FeatureCN,
                x.ReviewCN,
                x.CreatedAt,
                x.UserName,
                x.DisplayName,
                x.Email,
                x.Phone
            }).ToList();
            return Ok(new { total = result.Total, page = result.Page, pageSize = result.PageSize, items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取我的操作日志失败: page={Page}, pageSize={PageSize}, userId={UserId}", page, pageSize, userId);
            return StatusCode(500, new { message = "服务器内部错误" });
        }
    }

    private static LogFilterInput ParseFilter(string? filter)
    {
        var input = new LogFilterInput();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return input;
        }

        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filter);
            if (dict == null)
            {
                return input;
            }

            if (dict.TryGetValue("module", out var module) && module.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                input.Module = module.GetString();
            }

            if (dict.TryGetValue("feature", out var feature) && feature.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                input.Feature = feature.GetString();
            }

            if (dict.TryGetValue("type", out var type) && type.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                input.Type = type.GetString();
            }

            if (dict.TryGetValue("keyword", out var keyword) && keyword.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                input.Keyword = keyword.GetString();
            }

            if (dict.TryGetValue("dateRange", out var dateRange) &&
                dateRange.ValueKind == System.Text.Json.JsonValueKind.Array &&
                dateRange.GetArrayLength() == 2)
            {
                var from = dateRange[0].GetString();
                var to = dateRange[1].GetString();
                if (DateTime.TryParse(from, out var fromDateTime))
                {
                    input.From = fromDateTime;
                }
                if (DateTime.TryParse(to, out var toDateTime))
                {
                    input.To = toDateTime;
                }
            }
        }
        catch
        {
            // 忽略非法筛选参数，保持接口可用。
        }

        return input;
    }

    private sealed class LogFilterInput
    {
        public string? Module { get; set; }
        public string? Feature { get; set; }
        public string? Type { get; set; }
        public string? Keyword { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }
}


