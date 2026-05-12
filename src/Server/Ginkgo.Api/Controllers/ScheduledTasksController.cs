// 文件功能说明：
// 定时任务管理 REST API 控制器，提供任务列表、详情、配置更新、手动触发和执行日志查询。

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ginkgo.Application.Scheduling;

namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/scheduled-tasks")]
[ApiVersion("1.0")]
[Authorize]
public sealed class ScheduledTasksController : ControllerBase
{
    private readonly IScheduledTaskAppService _service;

    public ScheduledTasksController(IScheduledTaskAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取所有定时任务列表。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var tasks = await _service.GetAllTasksAsync(HttpContext.RequestAborted);
        return Ok(new { items = tasks, total = tasks.Count });
    }

    /// <summary>
    /// 获取指定任务详情。
    /// </summary>
    [HttpGet("{taskKey}")]
    public async Task<ActionResult> GetByKey(string taskKey)
    {
        var task = await _service.GetTaskByKeyAsync(taskKey, HttpContext.RequestAborted);
        if (task == null) return NotFound(new { message = $"任务 {taskKey} 不存在" });
        return Ok(task);
    }

    /// <summary>
    /// 更新任务配置（启禁用/Cron/描述）。
    /// </summary>
    [HttpPut("{taskKey}")]
    public async Task<ActionResult> Update(string taskKey, [FromBody] UpdateScheduledTaskInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CronExpression))
            return BadRequest(new { message = "Cron 表达式不能为空" });

        try
        {
            await _service.UpdateTaskAsync(taskKey, input, HttpContext.RequestAborted);
            return Ok(new { message = "更新成功" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 手动触发任务执行。
    /// </summary>
    [HttpPost("{taskKey}/trigger")]
    public async Task<ActionResult> Trigger(string taskKey)
    {
        try
        {
            await _service.TriggerTaskAsync(taskKey, HttpContext.RequestAborted);
            return Ok(new { message = $"任务 {taskKey} 已触发执行" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"任务执行失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 查询指定任务的执行日志（分页）。
    /// </summary>
    [HttpGet("{taskKey}/logs")]
    public async Task<ActionResult> GetLogs(string taskKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetTaskLogsAsync(taskKey, page, pageSize, HttpContext.RequestAborted);
        return Ok(new { items = result.Items, total = result.Total, page, pageSize });
    }

    /// <summary>
    /// 创建动态任务。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateDynamicTaskInput input)
    {
        try
        {
            var task = await _service.CreateDynamicTaskAsync(input, HttpContext.RequestAborted);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 删除动态任务。
    /// </summary>
    [HttpDelete("{taskKey}")]
    public async Task<ActionResult> Delete(string taskKey)
    {
        var result = await _service.DeleteDynamicTaskAsync(taskKey, HttpContext.RequestAborted);
        if (!result) return NotFound(new { message = $"任务 {taskKey} 不存在或不可删除（仅动态任务可删除）" });
        return Ok(new { message = "删除成功" });
    }

    /// <summary>
    /// 获取所有执行提供器列表（前端新增弹窗使用）。
    /// </summary>
    [HttpGet("execution-providers")]
    public ActionResult GetExecutionProviders()
    {
        var providers = _service.GetExecutionProviders();
        return Ok(new { items = providers });
    }

    /// <summary>
    /// 获取所有可调用动作列表（前端能力选择器使用）。
    /// </summary>
    [HttpGet("invocable-actions")]
    public ActionResult GetInvocableActions()
    {
        var actions = _service.GetInvocableActions();
        return Ok(new { items = actions });
    }

    /// <summary>
    /// 测试执行（不保存任务，仅验证并试运行）。
    /// </summary>
    [HttpPost("test-execute")]
    public async Task<ActionResult> TestExecute([FromBody] TestExecuteInput input)
    {
        try
        {
            var result = await _service.TestExecuteAsync(input.ExecutionSource, input.ConfigJson, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

/// <summary>
/// 测试执行输入。
/// </summary>
public sealed class TestExecuteInput
{
    public string ExecutionSource { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
}
