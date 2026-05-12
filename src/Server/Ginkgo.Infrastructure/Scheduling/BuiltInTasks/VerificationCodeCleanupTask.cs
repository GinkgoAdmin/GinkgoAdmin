// 文件功能说明：
// 内置定时任务：清理过期验证码记录。
// 调用已有的 IVerificationCodeRepository.DeleteExpiredAsync，
// 修复当前"方法存在但从未被定时调用"的问题。

using Ginkgo.Domain.Verification;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Infrastructure.Scheduling.BuiltInTasks;

/// <summary>
/// 内置定时任务：清理过期验证码记录。
/// </summary>
public sealed class VerificationCodeCleanupTask : IScheduledTask
{
    public string TaskKey => "System.VerificationCodeCleanup";
    public string DisplayName => "验证码过期清理";
    public string Group => "系统维护";
    public string CronExpression => "0 3 * * *"; // 每天凌晨3点
    public string? Description => "清理过期的验证码记录，防止数据堆积";
    public string ExecutionType => "内置方法";
    public string ExecutionTarget => "IVerificationCodeRepository.DeleteExpiredAsync → 删除 ginkgo_Sys_VerificationCode 中过期记录";

    public async Task ExecuteAsync(ScheduledTaskContext context)
    {
        var repo = context.Services.GetRequiredService<IVerificationCodeRepository>();
        await repo.DeleteExpiredAsync(DateTime.Now, context.CancellationToken);
        context.Logger.LogInformation("验证码过期清理完成");
    }
}
