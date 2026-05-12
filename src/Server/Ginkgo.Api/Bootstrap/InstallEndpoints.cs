using Microsoft.AspNetCore.Builder;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ginkgo.Api.Bootstrap;

public static class InstallEndpoints
{
    /// <summary>
    /// 映射安装模式下的最小端点集合：/install、/api/install/*、根重定向。
    /// 与 Program.cs 现有实现保持功能等价（仅抽取到此处）。
    /// </summary>
    public static void MapInstallationEndpoints(WebApplication app)
    {
        // /api/install/status
        app.MapGet("/api/install/status", (Ginkgo.Api.Install.InstallerService svc) =>
        {
            return Results.Ok(new { installed = svc.IsInstalled() });
        }).AllowAnonymous();

        // /install （安装页）
        app.MapGet("/install", (Ginkgo.Api.Install.InstallerService svc, IHostEnvironment env) =>
        {
            if (svc.IsInstalled()) return Results.Redirect("/swagger");
            var path = System.IO.Path.Combine(env.ContentRootPath, "Install", "install.html");
            if (!System.IO.File.Exists(path)) return Results.Problem(title: "安装页缺失", detail: path);
            var html = System.IO.File.ReadAllText(path);
            return Results.Content(html, "text/html; charset=utf-8");
        }).AllowAnonymous();

        // 根路径重定向至 /install
        app.MapGet("/", () => Results.Redirect("/install")).AllowAnonymous();

        // /api/install/providers —— 暴露当前已注册的所有数据库方言描述符（与 EndpointMapping 中等价）
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

        // /api/install/test-connection
        app.MapPost("/api/install/test-connection", async (
            [Microsoft.AspNetCore.Mvc.FromBody] Ginkgo.Api.Install.TestConnectionRequest input,
            Ginkgo.Infrastructure.Abstractions.IDialectRegistry dialectRegistry) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.Server) || string.IsNullOrWhiteSpace(input.Username))
                {
                    return Results.BadRequest(new { success = false, message = "服务器地址和用户名不能为空" });
                }

                Ginkgo.Infrastructure.Abstractions.IDatabaseDialect dialect;
                try { dialect = dialectRegistry.Get(input.Provider); }
                catch (Exception dex)
                {
                    return Results.BadRequest(new { success = false, message = dex.Message });
                }

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

        // /api/install/oneclick
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

        // /api/install/restart
        // 安装第一阶段完成后调用：先启动一个延时拉起当前可执行文件的分离子进程，
        // 再停止当前应用，从而实现"自动重启 API 进程"。
        app.MapPost("/api/install/restart", (Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime) =>
        {
            var scheduled = false;
            string? scheduleError = null;
            try
            {
                scheduled = SelfRestartHelper.TryScheduleSelfRestart(out scheduleError);
            }
            catch (Exception ex)
            {
                scheduleError = ex.Message;
                Console.WriteLine($"[BOOT] Schedule self-restart failed: {ex.Message}");
            }

            // 延迟一小段时间再停止当前进程，确保浏览器先收到响应。
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                try { lifetime.StopApplication(); }
                catch (Exception ex) { Console.WriteLine($"[BOOT] StopApplication failed: {ex.Message}"); }
            });

            return Results.Ok(new { restarting = true, autoRelaunch = scheduled, message = scheduleError });
        }).AllowAnonymous();

        // /api/install/start-frontend — SSE 接口：在 web 目录下执行 npm install + npm run dev
        app.MapGet("/api/install/start-frontend", async (IHostEnvironment env, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.ContentType = "text/event-stream; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            async Task SendSse(string evt, string data)
            {
                await ctx.Response.WriteAsync($"event: {evt}\ndata: {data}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
            }

            // 定位 web 目录（ContentRootPath = src/Server/Ginkgo.Api）
            var webDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "..", "web"));
            if (!Directory.Exists(webDir))
            {
                await SendSse("error", "未找到 web 目录：" + webDir);
                return;
            }

            await SendSse("log", "找到 web 目录：" + webDir);

            // 检测 npm / node 是否可用
            var npmCmd = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";

            // === 第一步：npm install ===
            await SendSse("phase", "npm_install");
            await SendSse("log", "正在执行 npm install（首次安装约需 1-3 分钟）...");

            var installOk = await RunProcessWithSse(npmCmd, "install", webDir, SendSse, ct);
            if (!installOk)
            {
                await SendSse("error", "npm install 执行失败，请检查 Node.js 是否已安装。");
                return;
            }
            await SendSse("log", "✓ npm install 完成");

            // === 第二步：npm run dev ===
            await SendSse("phase", "npm_run_dev");
            await SendSse("log", "正在启动前端开发服务 npm run dev ...");

            var psi = new ProcessStartInfo
            {
                FileName = npmCmd,
                Arguments = "run dev",
                WorkingDirectory = webDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            // 强制 Vite 输出颜色关闭以便解析
            psi.Environment["NO_COLOR"] = "1";
            psi.Environment["FORCE_COLOR"] = "0";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                await SendSse("error", "无法启动 npm run dev 进程");
                return;
            }

            // 持续读取输出，检测 Vite 启动成功的本地地址
            var urlDetected = false;
            var urlPattern = new Regex(@"https?://(?:localhost|127\.0\.0\.1|0\.0\.0\.0):\d+", RegexOptions.IgnoreCase);

            async Task ReadStream(System.IO.StreamReader reader)
            {
                try
                {
                    while (!ct.IsCancellationRequested && !reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line == null) break;

                        // 清理 ANSI 转义码
                        var cleanLine = Regex.Replace(line, @"\x1B\[[0-9;]*[a-zA-Z]", "").Trim();
                        if (string.IsNullOrWhiteSpace(cleanLine)) continue;

                        await SendSse("log", cleanLine);

                        if (!urlDetected)
                        {
                            var m = urlPattern.Match(cleanLine);
                            if (m.Success)
                            {
                                urlDetected = true;
                                await SendSse("ready", m.Value);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }

            var stdoutTask = ReadStream(proc.StandardOutput);
            var stderrTask = ReadStream(proc.StandardError);

            // 等待检测到 URL 或超时 120 秒
            var timeout = Task.Delay(TimeSpan.FromSeconds(120), ct);
            while (!urlDetected && !ct.IsCancellationRequested && !proc.HasExited)
            {
                if (await Task.WhenAny(Task.Delay(500, ct), timeout) == timeout)
                {
                    await SendSse("log", "等待前端服务启动超时（120 秒），请手动检查终端输出。");
                    break;
                }
            }

            if (!urlDetected && proc.HasExited)
            {
                await SendSse("error", "npm run dev 进程意外退出，退出码：" + proc.ExitCode);
            }

            // 注意：不 kill 进程，让 dev server 继续运行
        }).AllowAnonymous();
    }

    /// <summary>
    /// 运行一个短命令（如 npm install），实时推送输出，等待完成后返回是否成功。
    /// </summary>
    private static async Task<bool> RunProcessWithSse(
        string fileName, string arguments, string workingDirectory,
        Func<string, string, Task> sendSse, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        psi.Environment["NO_COLOR"] = "1";
        psi.Environment["FORCE_COLOR"] = "0";

        using var proc = Process.Start(psi);
        if (proc == null) return false;

        async Task Pump(System.IO.StreamReader reader)
        {
            try
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;
                    var clean = Regex.Replace(line, @"\x1B\[[0-9;]*[a-zA-Z]", "").Trim();
                    if (!string.IsNullOrWhiteSpace(clean))
                        await sendSse("log", clean);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        var t1 = Pump(proc.StandardOutput);
        var t2 = Pump(proc.StandardError);

        await proc.WaitForExitAsync(ct);
        await Task.WhenAll(t1, t2);

        return proc.ExitCode == 0;
    }

    // 注：进程自重启逻辑（TryScheduleSelfRestart / QuoteForCommandLine）已迁移到
    // Ginkgo.Api.Bootstrap.SelfRestartHelper，供 /api/install/restart 与
    // /api/v1/modules/restart-process 共享。
}
