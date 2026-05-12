using System.Diagnostics;
using Ginkgo.Domain.Modules;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 安全的 npm/pnpm/yarn 命令执行器。
/// 设计目标：彻底杜绝命令注入。即使插件作者在 module.json 的 npmDependencies 中嵌入恶意字符串，
/// 也无法跳出 npm 单一参数边界（不再走 cmd.exe /c，不再用 Arguments 字符串拼接）。
/// 实现要点：
/// 1. Windows 下不再使用 cmd.exe /c，改为通过 where.exe 解析 npm.cmd / pnpm.cmd / yarn.cmd 的绝对路径，
///    然后直接 spawn；这样 ProcessStartInfo 的 ArgumentList 不会被 cmd 解析元字符。
/// 2. 所有外部输入参数均通过 ArgumentList.Add 单独添加，由 .NET 自行做参数转义；调用方传入的字符串
///    不会被拆分成多个 token。
/// 3. 调用方在传入前必须先用 ModuleIdentifierValidator.IsSafeNpmPackageName / IsSafeNpmVersionSpec
///    做白名单校验，本类不再做语法兜底。
/// </summary>
public sealed class NpmCommandRunner
{
    /// <summary>
    /// 安全地执行 install &lt;packageSpec&gt;。
    /// </summary>
    /// <param name="packageManager">"npm" / "pnpm" / "yarn"</param>
    /// <param name="packageName">npm 包名，已通过 IsSafeNpmPackageName 校验</param>
    /// <param name="versionSpec">版本规范（语义化版本/range/dist-tag），已通过 IsSafeNpmVersionSpec 校验；空表示不指定</param>
    /// <param name="workDir">npm 执行的工作目录（应为 web 项目根）</param>
    /// <param name="extraFlags">额外 flag（如 --save、--save-dev），调用方需要确保是固定常量字符串</param>
    public async Task<(int ExitCode, string Output)> InstallAsync(
        string packageManager,
        string packageName,
        string? versionSpec,
        string workDir,
        IReadOnlyList<string>? extraFlags,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(packageManager)) throw new ArgumentException("packageManager required", nameof(packageManager));
        if (!ModuleIdentifierValidator.IsSafeNpmPackageName(packageName))
            throw new ArgumentException($"npm package name 不合法: {packageName}", nameof(packageName));
        if (!ModuleIdentifierValidator.IsSafeNpmVersionSpec(versionSpec))
            throw new ArgumentException($"npm version spec 不合法: {versionSpec}", nameof(versionSpec));

        var spec = string.IsNullOrEmpty(versionSpec) ? packageName : $"{packageName}@{versionSpec}";
        var args = new List<string> { "install", spec };
        if (extraFlags != null) args.AddRange(extraFlags);

        return await RunAsync(packageManager, args, workDir, ct);
    }

    /// <summary>
    /// 安全地执行 uninstall &lt;packageName&gt;。
    /// </summary>
    public async Task<(int ExitCode, string Output)> UninstallAsync(
        string packageManager,
        string packageName,
        string workDir,
        IReadOnlyList<string>? extraFlags,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(packageManager)) throw new ArgumentException("packageManager required", nameof(packageManager));
        if (!ModuleIdentifierValidator.IsSafeNpmPackageName(packageName))
            throw new ArgumentException($"npm package name 不合法: {packageName}", nameof(packageName));

        var args = new List<string> { "uninstall", packageName };
        if (extraFlags != null) args.AddRange(extraFlags);

        return await RunAsync(packageManager, args, workDir, ct);
    }

    private async Task<(int ExitCode, string Output)> RunAsync(
        string packageManager,
        IReadOnlyList<string> arguments,
        string workDir,
        CancellationToken ct)
    {
        var fileName = ResolveExecutable(packageManager);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // 用 ArgumentList 单独添加每个参数，由 .NET 处理引号与转义；外部输入无法跳出参数边界
        foreach (var a in arguments)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"无法启动 {packageManager} 进程");

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        return (process.ExitCode, output.Trim());
    }

    /// <summary>
    /// 解析包管理器可执行文件的绝对路径。
    /// Windows：通过 where.exe 找到 .cmd 入口；找不到时退化为直接 spawn `npm` 让 OS PATH 解析。
    /// 非 Windows：直接使用包管理器名（PATH 解析）。
    /// </summary>
    private static string ResolveExecutable(string packageManager)
    {
        if (!OperatingSystem.IsWindows())
            return packageManager;

        var candidates = new[] { $"{packageManager}.cmd", $"{packageManager}.exe", packageManager };
        foreach (var candidate in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add(candidate);
                using var p = Process.Start(psi);
                if (p == null) continue;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);
                if (p.ExitCode == 0)
                {
                    var first = output.Split('\n').FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(first) && File.Exists(first))
                        return first;
                }
            }
            catch
            {
                // 忽略，继续尝试下一个候选
            }
        }
        // 兜底：让 .NET 直接 spawn，PATH 内有则可用
        return packageManager;
    }
}
