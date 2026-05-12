// 文件功能说明：
// 内置定时任务：清理过期密码重置令牌。
// 调用已有的 IPasswordResetTokenRepository.DeleteExpiredAsync，
// 修复当前"方法存在但从未被定时调用"的问题。

using Ginkgo.Domain.Users;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Scheduling.BuiltInTasks;

/// <summary>
/// 内置定时任务：清理过期密码重置令牌。
/// </summary>
public sealed class PasswordResetTokenCleanupTask : IScheduledTask
{
    public string TaskKey => "System.PasswordResetTokenCleanup";
    public string DisplayName => "密码重置令牌清理";
    public string Group => "系统维护";
    public string CronExpression => "10 3 * * *"; // 每天凌晨3:10
    public string? Description => "清理过期的密码重置令牌";
    public string ExecutionType => "内置方法";
    public string ExecutionTarget => "IPasswordResetTokenRepository.DeleteExpiredAsync → 删除 ginkgo_Sys_PasswordResetToken 中过期记录";

    public async Task ExecuteAsync(ScheduledTaskContext context)
    {
        var repo = context.Services.GetRequiredService<IPasswordResetTokenRepository>();
        await repo.DeleteExpiredAsync(DateTime.Now, context.CancellationToken);
        context.Logger.LogInformation("密码重置令牌清理完成");
    }
}
