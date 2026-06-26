using Ginkgo.Application;
using Ginkgo.Application.Dictionaries;
using Ginkgo.Application.Menus;
using Ginkgo.Application.Messages;
using Ginkgo.Application.Modules;
using Ginkgo.Application.Notifications;
using Ginkgo.Application.Roles;
using Ginkgo.Application.Settings;
using Ginkgo.Application.Users;
using Ginkgo.Infrastructure.Persistence.Extensions;
using Ginkgo.Infrastructure.Runtime;
using Ginkgo.Infrastructure.Storage;
using Ginkgo.Api.Modules;
using Ginkgo.ServerToolkit;
using Ginkgo.ServerToolkit.AspNetCore;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Serilog;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 应用层服务、模块管理、文件存储注册（从 Program.cs 提取）。
/// </summary>
public static class ApplicationSetup
{
    /// <summary>
    /// 注册 ServerToolkit、模块管理、应用服务、DDD 通知系统。
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IServiceCollection builderServices, IConfiguration configuration)
    {
        // ServerToolkit 核心能力
        services.AddServerToolkit(configuration);
        services.AddServerToolkitAspNetCore();

        // 模块服务
        services.AddSingleton<InstalledModulesStore>(sp => new InstalledModulesStore(sp));
        services.AddSingleton<ModuleRepository>();
        services.AddSingleton<PendingDeleteManager>();
        services.AddSingleton<ModuleInstaller>();
        services.AddSingleton<ModuleSqlExecutor>();
        services.AddSingleton<ModuleRuntimeManager>();
        services.AddSingleton<ModuleLoader>(sp => new ModuleLoader(
            builderServices,
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ModuleRuntimeManager>(),
            sp.GetRequiredService<ApplicationPartManager>(),
            sp.GetRequiredService<MvcActionDescriptorChangeProvider>()));
        services.AddSingleton<ModuleHotReloader>();
        services.AddSingleton<ClientTaskService>();
        services.AddSingleton<SolutionManager>();
        services.AddSingleton<WebModuleManager>();
        services.AddSingleton<ServerModuleManager>();
        // 安全的 npm/pnpm/yarn 命令执行器：彻底杜绝命令注入（P0-3）
        services.AddSingleton<NpmCommandRunner>();
        services.AddSingleton<ModuleHashValidator>();
        services.AddSingleton<ModuleSignatureVerifier>();
        services.AddSingleton<ModuleCapabilityAuditor>();
        services.AddSingleton<ModuleGrayscaleService>();
        services.AddSingleton<ModuleSnapshotService>();
        services.AddSingleton<ModuleSecurityAuditService>();
        services.AddSingleton<ModuleDotnetBuildService>();
        services.AddSingleton<ModuleUploadService>();
        services.AddSingleton<ModulePackageService>();
        // license.lic 文件签名验证器（来自商城的 ECDSA 公钥）
        services.AddSingleton<LicenseFileVerifier>();

        // HttpClient 工厂
        services.AddHttpClient();

        // 插件商城远程连接专用 HttpClient（支持配置 SkipSslValidation 以兼容自签名/非标证书的远端商城）
        services.AddHttpClient("PluginStoreRemote")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var rawValue = config["PluginStore:SkipSslValidation"];
                var skipSsl = string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase);
                var handler = new HttpClientHandler
                {
                    // 兼容旧服务器：明确启用所有当前主流 TLS 版本，避免 TLS 协商阶段就失败
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
                if (skipSsl)
                {
                    handler.ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                    {
                        if (errors != System.Net.Security.SslPolicyErrors.None)
                        {
                            Log.Warning("[PluginStore] 跳过 HTTPS 证书校验: Host={Host}, Subject={Subject}, Errors={Errors}",
                                req.RequestUri?.Host, cert?.Subject, errors);
                        }
                        return true;
                    };
                }
                Log.Information("[PluginStore] HttpClient \"PluginStoreRemote\" 已构建。SkipSslValidation 配置值=\"{Raw}\"，实际生效={Effective}",
                    rawValue ?? "(null)", skipSsl);
                return handler;
            });

        // 注册模块适配器
        services.AddModules();
        // 注册应用层服务
        services.AddApplication();

        // 应用服务
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IRoleAppService, RoleAppService>();
        services.AddScoped<Ginkgo.Application.Departments.IDepartmentAppService, Ginkgo.Application.Departments.DepartmentAppService>();
        services.AddScoped<IMenuAppService, MenuAppService>();
        // 菜单组应用服务。MenuGroupAppService 与 RoleAppService 构造函数新增依赖
        // IRepository<RoleMenuGroupItem>（见 RoleMenuGroupItem 角色菜单组明细实体）。
        // 通用仓储已在 Ginkgo.Infrastructure 的 AddGinkgoPersistence() 中以开放泛型方式注册：
        //   services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        // 因此 IRepository<RoleMenuGroupItem> 会随任意实体类型自动解析，无需在此追加封闭泛型注册，
        // 也避免与开放泛型注册产生冲突。
        services.AddScoped<IMenuGroupAppService, MenuGroupAppService>();
        services.AddScoped<IDictionaryAppService, DictionaryAppService>();
        services.AddScoped<ISettingsAppService, SettingsAppService>();
        services.AddScoped<ModuleConfigDbService>();
        services.AddScoped<Ginkgo.Domain.Settings.IModuleConfigValueStore, Ginkgo.Infrastructure.Persistence.SqlSugar.ModuleConfigValueStore>();
        services.AddScoped<Ginkgo.Application.IDataPermissionService, Ginkgo.Application.DataPermissionService>();
        services.AddScoped<Ginkgo.Application.Files.IFileAppService, Ginkgo.Application.Files.FileAppService>();
        // DDD: 文件存储适配
        services.AddScoped<Ginkgo.Domain.Files.IFileContentStorage>(sp =>
            new Ginkgo.Infrastructure.Storage.FileContentStorageAdapter(sp.GetRequiredService<IFileStorageProvider>()));
        services.AddScoped<Ginkgo.Domain.Files.IFileDomainService, Ginkgo.Domain.Files.FileDomainService>();
        services.AddScoped<INotifyAppService, NotifyAppServiceV2>();
        services.AddScoped<IMessageAppService, MessageAppService>();

        // 数据范围解析器（供模块通过 DI 获取）
        // 注入 ISettingsRepository 以便运行时读取 DB Settings 表中的 DataPermission.* 覆盖 appsettings 配置；
        // 同时使解析器能桥接 DB Roles 表的 dataScope 字段到 RoleStrategies。
        services.AddScoped<Ginkgo.Infrastructure.Persistence.IDataScopeResolver>(sp =>
            new Ginkgo.Infrastructure.Persistence.DataScopeProvider(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Ginkgo.Infrastructure.Persistence.DataScopeOptions>>(),
                sp.GetRequiredService<SqlSugar.ISqlSugarClient>(),
                sp.GetRequiredService<Ginkgo.ServerToolkit.ICurrentUser>(),
                sp.GetService<Ginkgo.Domain.Settings.ISettingsRepository>()));

        // DDD 通知系统
        services.AddScoped<Ginkgo.Domain.Notifications.IAudienceResolver, DefaultAudienceResolver>();
        services.AddScoped<Ginkgo.Domain.Notifications.INotificationRepository,
            Ginkgo.Infrastructure.Persistence.SqlSugar.Notifications.NotificationRepository>();
        services.AddScoped<Ginkgo.Domain.Notifications.IAudienceRepository,
            Ginkgo.Infrastructure.Persistence.SqlSugar.Notifications.AudienceRepository>();
        services.AddScoped<INotificationAppService, NotificationAppService>();

        // 定时任务调度系统
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.ScheduledTaskRegistry>();
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.ActionRegistry>();
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.ExecutionProviderRegistry>();
        services.AddScoped<Ginkgo.Domain.Scheduling.IScheduledTaskRepository, Ginkgo.Infrastructure.Scheduling.ScheduledTaskRepository>();
        services.AddScoped<Ginkgo.Application.Scheduling.IScheduledTaskAppService, Ginkgo.Application.Scheduling.ScheduledTaskAppService>();
        // 注册内置任务
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.BuiltInTasks.VerificationCodeCleanupTask>();
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.BuiltInTasks.PasswordResetTokenCleanupTask>();
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.BuiltInTasks.RefreshTokenCleanupTask>();
        // 调度引擎（HostedService）
        services.AddSingleton<Ginkgo.Infrastructure.Scheduling.TaskSchedulerService>();
        services.AddHostedService(sp => sp.GetRequiredService<Ginkgo.Infrastructure.Scheduling.TaskSchedulerService>());

        return services;
    }

    /// <summary>
    /// 注册文件存储（本地实现，可切换），返回 uploadsRoot 路径供后续使用。
    /// </summary>
    public static string AddFileStorage(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var uploadsRoot = configuration["Upload:RootPath"];
        if (string.IsNullOrWhiteSpace(uploadsRoot))
        {
            var resourcePhysical = configuration["Resource:PhysicalPath"];
            if (!string.IsNullOrWhiteSpace(resourcePhysical) && OperatingSystem.IsLinux() && resourcePhysical.Contains('\\'))
            {
                resourcePhysical = null;
            }
            if (!string.IsNullOrWhiteSpace(resourcePhysical))
            {
                uploadsRoot = Path.Combine(resourcePhysical!, "uploads");
            }
            else
            {
                var contentResourceUploads = Path.Combine(contentRootPath, "resource", "uploads");
                if (Directory.Exists(Path.Combine(contentRootPath, "resource")))
                {
                    uploadsRoot = contentResourceUploads;
                }
                else
                {
                    uploadsRoot = Path.Combine(AppContext.BaseDirectory, "uploads");
                }
            }
        }
        if (!Path.IsPathRooted(uploadsRoot))
        {
            uploadsRoot = Path.GetFullPath(Path.Combine(contentRootPath, uploadsRoot));
        }
        services.AddSwitchable<IFileStorageProvider>(_ => new LocalFileStorageProvider(uploadsRoot));
        return uploadsRoot;
    }
}
