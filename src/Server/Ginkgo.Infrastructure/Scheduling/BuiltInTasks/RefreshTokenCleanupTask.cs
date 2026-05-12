// 文件功能说明：
// 内置定时任务：清理过期/已吊销的 RefreshToken。
// 修复当前 RefreshToken 无限堆积的问题。

using Ginkgo.Domain.Auth;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Ginkgo.Infrastructure.Scheduling.BuiltInTasks;

/// <summary>
/// 内置定时任务：清理过期或已吊销的刷新令牌。
/// </summary>
public sealed class RefreshTokenCleanupTask : IScheduledTask
{
    public string TaskKey => "System.RefreshTokenCleanup";
    public string DisplayName => "刷新令牌清理";
    public string Group => "系统维护";
    public string CronExpression => "0 4 * * *"; // 每天凌晨4点
    public string? Description => "清理过期或已吊销的刷新令牌，防止表无限增长";
    public string ExecutionType => "内置方法";
    public string ExecutionTarget => "直接 SQL 删除 ginkgo_Sys_RefreshToken 中过期超过 7 天或已吊销超过 7 天的记录";

    public async Task ExecuteAsync(ScheduledTaskContext context)
    {
        var db = context.Services.GetRequiredService<ISqlSugarClient>();
        var cutoff = DateTime.Now.AddDays(-7); // 保留7天内的记录
        var deleted = await db.Deleteable<RefreshToken>()
            .Where(t => t.ExpiresAt < cutoff || (t.IsRevoked && t.RevokedAt < cutoff))
            .ExecuteCommandAsync(context.CancellationToken);
        context.Logger.LogInformation("刷新令牌清理完成，删除 {Count} 条记录", deleted);
    }
}
