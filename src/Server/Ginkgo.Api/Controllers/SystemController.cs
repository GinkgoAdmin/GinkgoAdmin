using Ginkgo.Realtime;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 系统级监控端点（仅管理员可访问）。
/// </summary>
[ApiController]
[Route("api/system")]
[Authorize(Roles = "ADMIN")]
public sealed class SystemController : ControllerBase
{
    /// <summary>
    /// 获取队列度量指标。
    /// </summary>
    [HttpGet("queue-metrics")]
    public Result<object> GetQueueMetrics([FromServices] IQueuePublisher publisher)
    {
        // InMemoryQueue 同时实现了 IQueuePublisher 和暴露 Metrics
        if (publisher is InMemoryQueue queue)
        {
            var m = queue.Metrics;
            return Result<object>.Success(new
            {
                m.Published,
                m.Consumed,
                m.Failed,
                m.Retried,
                m.DeadLettered
            });
        }
        return Result<object>.Success(new { message = "当前队列实现不支持度量指标" });
    }
}
