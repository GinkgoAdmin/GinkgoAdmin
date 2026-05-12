using Microsoft.AspNetCore.Mvc;
using Ginkgo.Domain;
using Ginkgo.Domain.Users;
using Ginkgo.Domain.Departments;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Logs;
using SqlSugar;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 后台首页仪表盘数据接口
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class DashboardController : ControllerBase
{
    private readonly ISqlSugarClient _db;

    public DashboardController(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取首页统计概览
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var now = DateTime.Now;
        var todayStart = now.Date;
        var yesterdayStart = todayStart.AddDays(-1);

        // 基础统计
        var totalUsers = await _db.Queryable<User>().CountAsync();
        var totalRoles = await _db.Queryable<Role>().CountAsync();
        var totalDepts = await _db.Queryable<Department>().CountAsync();
        var totalFiles = await _db.Ado.GetIntAsync("SELECT COUNT(*) FROM ginkgo_Sys_File");
        var totalModules = await _db.Ado.GetIntAsync("SELECT COUNT(*) FROM ginkgo_Modules_Installed WHERE Enabled = 1");

        // 今日操作日志
        var todayLogs = await _db.Queryable<OpLog>()
            .Where(x => x.CreatedAt >= todayStart)
            .CountAsync();

        // 昨日操作日志（用于趋势对比）
        var yesterdayLogs = await _db.Queryable<OpLog>()
            .Where(x => x.CreatedAt >= yesterdayStart && x.CreatedAt < todayStart)
            .CountAsync();

        // 七日内新增用户
        var recentNewUsers = await _db.Queryable<User>()
            .Where(x => x.CreatedAt >= todayStart.AddDays(-7))
            .CountAsync();

        // 总通知数
        var totalNotifications = await _db.Ado.GetIntAsync("SELECT COUNT(*) FROM ginkgo_Sys_NotifyMessage");

        return Ok(new
        {
            totalUsers,
            totalRoles,
            totalDepts,
            totalFiles,
            totalModules,
            todayLogs,
            yesterdayLogs,
            recentNewUsers,
            totalNotifications
        });
    }

    /// <summary>
    /// 获取操作日志趋势（按天统计）
    /// </summary>
    [HttpGet("log-trend")]
    public async Task<IActionResult> GetLogTrend([FromQuery] int days = 7)
    {
        if (days < 1) days = 7;
        if (days > 90) days = 90;

        var startDate = DateTime.Now.Date.AddDays(-days + 1);

        var rawData = await _db.Queryable<OpLog>()
            .Where(x => x.CreatedAt >= startDate)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(x => new { date = x.CreatedAt.Date, count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync();

        // 确保每一天都有数据（包括 0 的天）
        var result = new List<object>();
        for (int i = 0; i < days; i++)
        {
            var d = startDate.AddDays(i);
            var cnt = rawData.FirstOrDefault(x => x.date.Date == d.Date)?.count ?? 0;
            result.Add(new { date = d.ToString("MM-dd"), count = cnt });
        }

        return Ok(result);
    }

    /// <summary>
    /// 获取最近操作日志（首页活动流）
    /// </summary>
    [HttpGet("recent-activities")]
    public async Task<IActionResult> GetRecentActivities([FromQuery] int limit = 10)
    {
        if (limit < 1) limit = 10;
        if (limit > 50) limit = 50;

        var logs = await _db.Queryable<OpLog, User>((log, user) =>
                new JoinQueryInfos(JoinType.Left, log.UserId == user.Id))
            .Where((log, user) => log.Action != "EXIT" && log.Action != "GET")
            .OrderByDescending((log, user) => log.CreatedAt)
            .Take(limit)
            .Select((log, user) => new
            {
                id = log.Id,
                action = log.Action,
                resource = log.Resource,
                moduleCN = log.ModuleCN,
                featureCN = log.FeatureCN,
                reviewCN = log.ReviewCN,
                userName = user.DisplayName ?? user.UserName,
                createdAt = log.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// 获取用户注册趋势（按天统计）
    /// </summary>
    [HttpGet("user-trend")]
    public async Task<IActionResult> GetUserTrend([FromQuery] int days = 7)
    {
        if (days < 1) days = 7;
        if (days > 90) days = 90;

        var startDate = DateTime.Now.Date.AddDays(-days + 1);

        var rawData = await _db.Queryable<User>()
            .Where(x => x.CreatedAt >= startDate)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(x => new { date = x.CreatedAt.Date, count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync();

        var result = new List<object>();
        for (int i = 0; i < days; i++)
        {
            var d = startDate.AddDays(i);
            var cnt = rawData.FirstOrDefault(x => x.date.Date == d.Date)?.count ?? 0;
            result.Add(new { date = d.ToString("MM-dd"), count = cnt });
        }

        return Ok(result);
    }
}
