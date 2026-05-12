// 应用生命周期钩子：启动后回调（模块 OnLoad、定时任务注册）与退出日志。

using Ginkgo.Api.Modules;
using Ginkgo.Domain;
using Ginkgo.Domain.Logs;
using Ginkgo.Infrastructure.Runtime;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 应用生命周期钩子：ApplicationStarted / ApplicationStopping 回调注册。
/// </summary>
public static class LifecycleHooks
{
    /// <summary>
    /// 注册 ApplicationStopping 退出日志。
    /// </summary>
    public static void RegisterExitLogging(this WebApplication app)
    {
        app.Lifetime.ApplicationStopping.Register(async () =>
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRepository<OpLog>>();
                var now = DateTime.Now;
                await repo.AddAsync(new OpLog
                {
                    Action = "EXIT",
                    Resource = "app/stop",
                    ModuleCN = "系统",
                    FeatureCN = "关闭",
                    Result = "OK",
                    At = now,  // 操作时间（数据库 NOT NULL）
                    CreatedAt = now
                });
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] Exit log write failed: {ex.Message}"); }
        });
    }

    /// <summary>
    /// 注册 ApplicationStarted 回调：模块 OnLoad、定时任务注册、执行提供器注册。
    /// </summary>
    public static void RegisterPostStartupHooks(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(async () =>
        {
            // 回调已加载模块的 OnLoadAsync（若有）
            try
            {
                using var scope = app.Services.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<ModuleRuntimeManager>();
                await runtime.OnAppStartedAsync(scope.ServiceProvider);
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] OnAppStartedAsync failed: {ex.Message}"); }

            // 注册内置定时任务到 Registry
            try
            {
                var registry = app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry>();
                registry.Register(app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.BuiltInTasks.VerificationCodeCleanupTask>());
                registry.Register(app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.BuiltInTasks.PasswordResetTokenCleanupTask>());
                registry.Register(app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.BuiltInTasks.RefreshTokenCleanupTask>());
                Console.WriteLine("[BOOT] 已注册 3 个内置定时任务");
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] RegisterBuiltInTasks failed: {ex.Message}"); }

            // 桥接内置定时任务为可调用动作（IInvocableAction）并注册到 ActionRegistry
            try
            {
                var taskRegistry = app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry>();
                var actionRegistry = app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.ActionRegistry>();
                foreach (var reg in taskRegistry.GetAll())
                {
                    var bridge = new Ginkgo.Infrastructure.Scheduling.ScheduledTaskActionBridge(reg.Task);
                    actionRegistry.Register(bridge, reg.Source, reg.SourceDisplayName);
                }
                Console.WriteLine($"[BOOT] 已桥接 {taskRegistry.GetAll().Count} 个内置任务为可调用动作");
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] BridgeActionsFromTasks failed: {ex.Message}"); }

            // 注册内置执行提供器到 ExecutionProviderRegistry
            try
            {
                var providerRegistry = app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.ExecutionProviderRegistry>();
                var actionRegistry = app.Services.GetRequiredService<Ginkgo.Infrastructure.Scheduling.ActionRegistry>();
                providerRegistry.Register(new Ginkgo.Infrastructure.Scheduling.Providers.ActionExecutionProvider(actionRegistry));
                providerRegistry.Register(new Ginkgo.Infrastructure.Scheduling.Providers.HttpExecutionProvider());
                providerRegistry.Register(new Ginkgo.Infrastructure.Scheduling.Providers.SqlExecutionProvider());
                Console.WriteLine("[BOOT] 已注册 3 个内置执行提供器（Action/Http/Sql）");
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] RegisterExecutionProviders failed: {ex.Message}"); }
        });
    }
}
