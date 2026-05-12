// 文件功能说明：
// WebAPI 入口：采用模块化 Bootstrap 类注册服务，保持 Program.cs 专注于编排。

using Ginkgo.Api.Modules;
using Ginkgo.Api.Middlewares;
using Serilog;

using Ginkgo.Api.Bootstrap;
var builder = WebApplication.CreateBuilder(args);

// Serilog: 从配置读取（支持 Console + File 滚动）
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// 捕获未处理异常，确保控制台能看到完整堆栈
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    try
    {
        Console.Error.WriteLine("[FATAL] Unhandled exception:");
        Console.Error.WriteLine(e.ExceptionObject?.ToString());
    }
    catch { /* swallow: last-resort handler, Console itself may be broken */ }
};

// 配置加载（jsonc 支持 + db.json 运行时配置）
builder.AddJsoncConfiguration();
builder.AddDatabaseJsonConfiguration();

// Kestrel 自托管 URL 配置
builder.ConfigureKestrelUrls();

// 安装模式检测
var (installationMode, resourceDirToUse) = builder.DetectInstallationMode();

// 初始化 Snowflake ID 生成器（尽早初始化，避免插件加载时触发抛出异常）
try
{
    var snowflakeConfig = new Ginkgo.Domain.Utils.SnowflakeConfig();
    builder.Configuration.GetSection(Ginkgo.Domain.Utils.SnowflakeConfig.SectionName).Bind(snowflakeConfig);
    Ginkgo.Domain.Utils.SnowflakeIdGenerator.Initialize(snowflakeConfig);
    Console.WriteLine($"[BOOT] Snowflake ID generator initialized with MachineId: {snowflakeConfig.GetEffectiveMachineId()}");
}
catch (Exception ex)
{
    Console.WriteLine($"[BOOT] Failed to initialize Snowflake ID generator: {ex.Message}");
    throw; // Snowflake ID 生成器初始化失败是致命错误
}

// ===================== 服务注册 =====================

// 持久化（数据库）
builder.Services.AddPersistence(builder.Configuration);

// 安装服务
builder.Services.AddSingleton<Ginkgo.Api.Install.InstallerService>();

// 实时通信（SignalR / 消息队列）
if (!installationMode)
{
    builder.Services.AddRealtimeServices(builder.Configuration);
}

// API 核心（Controllers/Swagger/CORS/HealthCheck 等）
builder.Services.AddApiCore(builder.Configuration);

// JWT 认证与授权
builder.Services.AddJwtAuthentication(builder.Configuration);
if (!installationMode)
{
    builder.Services.AddPermissionAuthorization();
}

// 应用服务 + 模块管理
if (!installationMode)
{
    builder.Services.AddApplicationServices(builder.Services, builder.Configuration);
}

// 文件存储
var uploadsRoot = "";
if (!installationMode)
{
    uploadsRoot = builder.Services.AddFileStorage(builder.Configuration, builder.Environment.ContentRootPath);
}

// 清理上次卸载时无法删除的模块目录。必须在模块预加载前执行，避免残留 DLL 被再次加载后继续锁定。
try
{
    PendingDeleteManager.CleanupPendingDeletes(AppContext.BaseDirectory, Console.WriteLine);
}
catch (Exception ex)
{
    Console.WriteLine($"[BOOT] PendingDeleteManager cleanup failed: {ex.Message}");
}

// ===================== 模块预加载 =====================

var preload = builder.PreloadModules(installationMode);

// ===================== Build =====================

Console.WriteLine("[BOOT] Building app...");
WebApplication app = builder.Build();
Console.WriteLine("[BOOT] Build completed.");

// ===================== 中间件管道 =====================

Console.WriteLine("[BOOT] Entering pipeline configuration...");

// 安装模式：最小化 HTTP 管道，启动后直接返回
if (installationMode)
{
    Console.WriteLine("[BOOT] Installation mode: configuring minimal pipeline...");

    // RequestId
    app.Use(async (ctx, next) =>
    {
        if (!ctx.Request.Headers.TryGetValue("X-Request-Id", out var rid) || string.IsNullOrWhiteSpace(rid))
        {
            rid = Ginkgo.Domain.Utils.SequentialGuid.NewGuid().ToString("N");
            ctx.Request.Headers["X-Request-Id"] = rid;
        }
        ctx.Response.Headers["X-Request-Id"] = rid;
        await next();
    });

    // Basic middlewares
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
    });
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseCors("ConfiguredCors");
    app.UseAuthentication();

    // Swagger（安装模式下也启用，避免安装完成后跳转 /swagger 出现 404）
    app.UseSwagger();
    app.UseSwaggerUI();

    // 安装端点
    InstallEndpoints.MapInstallationEndpoints(app);

    Console.WriteLine("[BOOT] Installation mode: starting Kestrel...");
    await app.RunAsync();
    Console.WriteLine("[BOOT] app.RunAsync() returned (installation mode) — exiting.");
    return;
}

// ===================== 正常模式管道 =====================

// 静态文件托管（必须在 UseRouting/UseAuthorization 之前）
app.UseStaticFileMappings(builder.Configuration, uploadsRoot, installationMode);
app.UseSpaStaticFiles(); // Host Vue SPA from wwwroot/ if present
app.UseH5StaticFiles();  // Host UniApp H5 from h5/ if present

// 请求管道（RequestId/Serilog/错误处理/CORS/Auth 等）
app.UseRequestPipeline(builder.Configuration, installationMode);

// 开发模块 MVC 控制器注册
app.RegisterDevModuleMvcParts(preload);

// 模块运行时注册（建表 + RegisterKnown + TryLoad + InstalledModulesStore 同步）
app.RegisterAndLoadModules(preload);

// 端点映射（Health/Hub/Controller/匿名路由/安装端点/管理端点）
app.MapApplicationEndpoints(installationMode);

// 退出日志
app.RegisterExitLogging();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// 启动后钩子（模块 OnLoad + 定时任务 + 执行提供器）
app.RegisterPostStartupHooks();

Console.WriteLine("[BOOT] End of pipeline configuration reached.");

// SPA fallback: must be AFTER all API/controller routes so they take priority
app.MapH5Fallback();   // /h5/** -> h5/index.html (UniApp H5)
app.MapSpaFallback();  // /** -> wwwroot/index.html (Vue Web)

Console.WriteLine("[BOOT] Starting Kestrel (app.RunAsync)...");
await app.RunAsync();
Console.WriteLine("[BOOT] app.RunAsync() returned.");
