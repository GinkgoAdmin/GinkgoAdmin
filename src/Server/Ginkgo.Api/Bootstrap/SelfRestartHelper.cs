using System.Diagnostics;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 进程自重启辅助类。
/// 通过启动一个分离的子进程（Windows 走 cmd /c timeout + start，Linux/macOS 走 bash + nohup &），
/// 在延迟数秒后用 <see cref="Environment.ProcessPath"/> 加上原始命令行参数再次拉起本进程；
/// 同时调用方在响应返回后调用 IHostApplicationLifetime.StopApplication() 关闭当前实例，
/// 从而实现 “无外部守护、单进程级别” 的自重启。
///
/// 使用场景：
/// 1. /api/install/restart：安装完成后让 API 进程载入新的 resource/db.json。
/// 2. /api/v1/modules/restart-process：开发模式下「重启服务并重载插件」按钮，
///    用于让 Vite/ALC 完整重新扫描 modules 目录、加载新插件 DLL。
///
/// 注意事项：
/// - 仅适用于 Console / Kestrel 直接启动的场景；运行在 IIS / systemd / docker 等带守护的部署下，
///   stop 后由守护进程自动拉起，本辅助类的子进程会失败但不影响最终重启效果。
/// - <see cref="TryScheduleSelfRestart"/> 必须在 StopApplication 之前调用，否则父进程退出后无法启动子进程。
/// </summary>
internal static class SelfRestartHelper
{
    /// <summary>
    /// 启动一个分离的子进程，等待数秒后用当前可执行文件 + 原始命令行参数再次拉起本进程。
    /// </summary>
    /// <param name="error">失败时返回的错误信息</param>
    /// <returns>是否成功调度到子进程</returns>
    public static bool TryScheduleSelfRestart(out string? error)
    {
        error = null;
        var procPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(procPath) || !File.Exists(procPath))
        {
            error = "无法解析 Environment.ProcessPath，无法自重启";
            return false;
        }

        // Environment.GetCommandLineArgs()[0] 即当前可执行文件，从下标 1 开始才是原始参数
        var args = Environment.GetCommandLineArgs();
        var restArgs = args.Length > 1
            ? string.Join(" ", args.Skip(1).Select(QuoteForCommandLine))
            : string.Empty;
        var workDir = Environment.CurrentDirectory;

        try
        {
            ProcessStartInfo psi;
            if (OperatingSystem.IsWindows())
            {
                // 使用 cmd 的 timeout + start 让新实例从 cmd 中分离，并延迟启动。
                // start 的第一个带引号参数会被当成窗口标题，所以这里显式传入标题占位。
                var startCmd = $"/c timeout /t 3 /nobreak > nul && start \"GinkgoAdminApi\" /D \"{workDir}\" \"{procPath}\" {restArgs}";
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = startCmd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workDir
                };
            }
            else
            {
                // Linux / macOS：用 nohup + & 让新进程脱离当前会话
                var script = $"sleep 3 && nohup \"{procPath}\" {restArgs} > /dev/null 2>&1 &";
                psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{script.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir
                };
            }

            var p = Process.Start(psi);
            if (p == null)
            {
                error = "无法启动重启调度进程";
                return false;
            }
            Console.WriteLine($"[BOOT] Self-restart scheduled. Target: {procPath}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 命令行参数转义：含空格/引号的参数加引号并对内部引号转义。
    /// </summary>
    private static string QuoteForCommandLine(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return arg;
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}
