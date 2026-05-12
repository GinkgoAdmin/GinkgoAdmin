// 端点映射：Health Check、Hub、Controller 路由、匿名路由、安装端点等。

using System.Data.Common;
using Ginkgo.Realtime;
using Microsoft.AspNetCore.Authorization;
using SqlSugar;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 端点映射：Health Check、SignalR Hub、Controller、安装端点等。
/// </summary>
public static class EndpointMapping
{
    /// <summary>
    /// 映射所有应用端点（Health Check、Hub、Controller、匿名路由、安装端点、管理端点）。
    /// </summary>
    public static void MapApplicationEndpoints(this WebApplication app, bool installationMode)
    {
        // 健康检查端点（始终映射）
        MapHealthCheck(app);

        if (!installationMode)
        {
            // SignalR Hub
            // 关键：Hub 端点必须 AllowAnonymous，否则全局 FallbackPolicy 会拦下登录前的 negotiate 请求。
            // Hub 内部仍可通过 Context.User 判断未登录连接并断开，且 OnMessageReceived 已支持 query 参数 access_token。
            app.MapHub<NotifyHub>("/hubs/notify").RequireCors("ConfiguredCors").AllowAnonymous();
            // 控制器默认要求 Permission 策略；控制器/动作上标注 [AllowAnonymous] 可放行
            app.MapControllers().RequireAuthorization("Permission").RequireCors("ConfiguredCors");
            // 匿名路由放行
            MapAnonymousRoutes(app);
            // 数据库连接检查
            MapDbCheck(app);
        }

        // 安装相关端点（部分在非安装模式下也需映射）
        MapInstallEndpoints(app, installationMode);

        // 管理员维护端点
        if (!installationMode)
        {
            DatabaseMaintenanceService.MapAdminEndpoints(app);
        }
    }

    /// <summary>
    /// 映射健康检查端点 /health。
    /// </summary>
    private static void MapHealthCheck(WebApplication app)
    {
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.Now,
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds
                    })
                });
                await context.Response.WriteAsync(result);
            }
        }).AllowAnonymous();
    }

    /// <summary>
    /// 映射需要匿名访问的控制器路由。
    /// </summary>
    private static void MapAnonymousRoutes(WebApplication app)
    {
        // 放通"我的通知"读取（角标等匿名可调用；生产可改为鉴权）
        app.MapControllerRoute(
            name: "my-notify",
            pattern: "api/v{version:apiVersion}/notifications/my/{**catchAll}")
           .WithMetadata(new AllowAnonymousAttribute())
           .RequireCors("ConfiguredCors");
        // 放通 /users/me：方法内部自行判断是否登录（便于前端在无权限策略下获取"本人"信息）
        app.MapControllerRoute(
            name: "me",
            pattern: "api/v{version:apiVersion}/users/me")
           .WithMetadata(new AllowAnonymousAttribute())
           .RequireCors("ConfiguredCors");
        app.MapControllerRoute(
            name: "settings-get",
            pattern: "api/v{version:apiVersion}/settings")
           .WithMetadata(new AllowAnonymousAttribute())
           .RequireCors("ConfiguredCors");
        app.MapControllerRoute(
            name: "my-logs",
            pattern: "api/v{version:apiVersion}/logs/my")
           .WithMetadata(new AllowAnonymousAttribute())
           .RequireCors("ConfiguredCors");

        // 模块客户端接口放通匿名（客户端 Agent 可在无菜单权限时拉取包与任务）
        app.MapControllerRoute(
            name: "modules-client",
            pattern: "api/v{version:apiVersion}/modules/{**catchAll}")
           .WithMetadata(new AllowAnonymousAttribute())
           .RequireCors("ConfiguredCors");
    }

    /// <summary>
    /// 数据库连接检查端点：使用 SqlSugar 的 ADO 打开连接。
    /// </summary>
    private static void MapDbCheck(WebApplication app)
    {
        app.MapGet("/dbcheck", async (ISqlSugarClient sugar) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var conn = sugar.Ado.Connection; // 获取底层连接
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    if (conn is DbConnection dbConn)
                    {
                        await dbConn.OpenAsync(cts.Token);
                    }
                    else
                    {
                        conn.Open();
                    }
                }
                return Results.Ok(new { connected = true });
            }
            catch (OperationCanceledException)
            {
                return Results.Problem(title: "数据库连接超时", detail: "5秒内未能建立连接", statusCode: 504);
            }
            catch (Exception ex)
            {
                return Results.Problem(title: "数据库连接错误", detail: ex.Message);
            }
        }).AllowAnonymous();
    }

    /// <summary>
    /// 安装相关端点（状态查询、安装页面、测试连接、一键安装、重启）。
    /// </summary>
    private static void MapInstallEndpoints(WebApplication app, bool installationMode)
    {
        // -------------------- First-time install minimal endpoints --------------------
        app.MapGet("/api/install/status", (Ginkgo.Api.Install.InstallerService svc) =>
        {
            return Results.Ok(new { installed = svc.IsInstalled() });
        }).AllowAnonymous();

        app.MapGet("/install", (Ginkgo.Api.Install.InstallerService svc, IHostEnvironment env) =>
        {
            if (svc.IsInstalled()) return Results.Redirect("/swagger");
            var path = System.IO.Path.Combine(env.ContentRootPath, "Install", "install.html");
            if (!System.IO.File.Exists(path)) return Results.Problem(title: "安装页缺失", detail: path);
            var html = System.IO.File.ReadAllText(path);
            return Results.Content(html, "text/html; charset=utf-8");
        }).AllowAnonymous();

        // 安装模式下访问根路径自动跳转到安装页面
        if (installationMode)
        {
            app.MapGet("/", () => Results.Redirect("/install")).AllowAnonymous();
            // 处理 POST 到根路径的请求（可能来自健康检查或监控工具）
            app.MapPost("/", () => Results.Ok(new { status = "ok", mode = "installation" })).AllowAnonymous();
        }

        // /api/install/providers —— 暴露当前已注册的所有数据库方言描述符
        // 前端安装向导据此动态渲染数据库类型下拉、默认端口与连接串模板，
        // 杜绝在 HTML/JS 中再次硬编码 MySQL/SQL Server 字面量。
        app.MapGet("/api/install/providers", (
            Ginkgo.Infrastructure.Abstractions.IDialectRegistry dialectRegistry) =>
        {
            var providers = dialectRegistry.List()
                .Select(d => new
                {
                    code = d.Code,
                    displayName = d.DisplayName,
                    defaultPort = d.DefaultPort,
                    connectionStringTemplate = d.ConnectionStringTemplate
                })
                .ToList();
            return Results.Ok(new { providers });
        }).AllowAnonymous();

        app.MapPost("/api/install/test-connection", async (
            [Microsoft.AspNetCore.Mvc.FromBody] Ginkgo.Api.Install.TestConnectionRequest input,
            Ginkgo.Api.Install.InstallerService installerSvc,
            Ginkgo.Infrastructure.Abstractions.IDialectRegistry dialectRegistry) =>
        {
            // 🔒 安全修复：系统已安装后禁止调用此端点，防止 SSRF 探测内网数据库
            if (installerSvc.IsInstalled())
            {
                return Results.Json(new { success = false, message = "系统已安装，此端点已禁用" }, statusCode: 403);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(input.Server) || string.IsNullOrWhiteSpace(input.Username))
                {
                    return Results.BadRequest(new { success = false, message = "服务器地址和用户名不能为空" });
                }

                // 解析方言，未注册的 provider 会抛 InvalidOperationException（异常信息含已注册清单）
                Ginkgo.Infrastructure.Abstractions.IDatabaseDialect dialect;
                try { dialect = dialectRegistry.Get(input.Provider); }
                catch (Exception dex)
                {
                    return Results.BadRequest(new { success = false, message = dex.Message });
                }

                // 由方言构建测试连接串（host/port/user/password → 方言专属连接串模板）
                var connString = dialect.BuildTestConnectionString(input.Server, input.Port, input.Username, input.Password ?? string.Empty);

                using var conn = dialect.CreateConnection(connString);
                await conn.OpenAsync();

                return Results.Ok(new { success = true, message = "数据库连接测试成功" });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, message = $"连接失败: {ex.Message}" });
            }
        }).AllowAnonymous();

        // 安装成功后触发应用重启，以便按新配置（resource/db.json）正常启动
        if (installationMode)
        {
            app.MapPost("/api/install/restart", (Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime) =>
            {
                try { lifetime.StopApplication(); } catch (Exception ex) { Console.WriteLine($"[BOOT] StopApplication failed: {ex.Message}"); }
                return Results.Ok(new { restarting = true });
            }).AllowAnonymous();
        }

        app.MapPost("/api/install/oneclick", async (
            Ginkgo.Api.Install.InstallerService svc,
            [Microsoft.AspNetCore.Mvc.FromBody] Ginkgo.Api.Install.InstallRequest input,
            CancellationToken ct) =>
        {
            if (svc.IsInstalled())
            {
                return Results.Conflict(new { message = "系统已安装，如需重新安装请删除 resource/install.lock 后重启。" });
            }
            var res = await svc.InstallAsync(input, ct);
            var status = res.Success ? 200 : 400;
            return Results.Json(res, statusCode: status);
        }).AllowAnonymous();
        // ------------------------------------------------------------------------------------
    }
}
