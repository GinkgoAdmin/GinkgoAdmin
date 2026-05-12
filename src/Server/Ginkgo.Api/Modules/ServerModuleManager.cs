using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

public class ServerModuleManager
{
    private readonly ILogger<ServerModuleManager> _logger;
    private readonly WebModuleManager _webModuleManager;

    public ServerModuleManager(ILogger<ServerModuleManager> logger, WebModuleManager webModuleManager)
    {
        _logger = logger;
        _webModuleManager = webModuleManager;
    }

    /// <summary>
    /// 从 module.json 中解析 nugetDependencies 数组
    /// 格式：[ { "name": "PackageA", "version": "1.0.0" } ]
    /// 或者解析 fallback .csproj 中的 PackageReference
    /// </summary>
    private static List<(string name, string version)> ParseNugetDependencies(string moduleJsonPath)
    {
        var result = new List<(string name, string version)>();
        if (!File.Exists(moduleJsonPath)) return result;

        try
        {
            var json = File.ReadAllText(moduleJsonPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("nugetDependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    if (dep.TryGetProperty("name", out var n) && dep.TryGetProperty("version", out var v))
                    {
                        var name = n.GetString();
                        var version = v.GetString();
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version))
                        {
                            result.Add((name, version));
                        }
                    }
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// 扫描所有已安装插件的 module.json，收集它们声明的 nuget 依赖 (Reference Counting)
    /// </summary>
    private static HashSet<string> CollectAllPluginNugetDeps(string installModeModulesBaseDir, string srcSearchPath, string? excludePlugin = null)
    {
        var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scan the standard modules/ directory
        if (Directory.Exists(installModeModulesBaseDir))
        {
            foreach (var pluginDir in Directory.GetDirectories(installModeModulesBaseDir))
            {
                var pName = Path.GetFileName(pluginDir);
                if (excludePlugin != null && string.Equals(pName, excludePlugin, StringComparison.OrdinalIgnoreCase)) continue;

                var jsonFile = Path.Combine(pluginDir, "server", "module.json");
                if (File.Exists(jsonFile))
                {
                    foreach (var (name, _) in ParseNugetDependencies(jsonFile))
                    {
                        allDeps.Add(name);
                    }
                }
            }
        }

        // Scan local src/Module directory (for source mode)
        if (Directory.Exists(srcSearchPath))
        {
            foreach (var pluginDir in Directory.GetDirectories(srcSearchPath))
            {
                var pName = Path.GetFileName(pluginDir);
                if (excludePlugin != null && string.Equals(pName, excludePlugin, StringComparison.OrdinalIgnoreCase)) continue;

                var jsonFile = Path.Combine(pluginDir, "server", "module.json");
                if (File.Exists(jsonFile))
                {
                    foreach (var (name, _) in ParseNugetDependencies(jsonFile))
                    {
                        allDeps.Add(name);
                    }
                }
            }
        }

        return allDeps;
    }

    public async Task InstallNugetDependenciesAsync(string moduleJsonPath, string hostCsprojPath, CancellationToken ct)
    {
        if (!File.Exists(hostCsprojPath)) return;

        var deps = ParseNugetDependencies(moduleJsonPath);
        if (deps.Count == 0) return;

        // Skip if package already in hostCsprojPath? Ideally dotnet add handles idempotent additions.
        foreach (var (name, version) in deps)
        {
            _logger.LogInformation("[ServerModuleManager] 安装 NuGet 包: {Package} @ {Version}", name, version);
            var success = await RunDotnetCommandAsync(Path.GetDirectoryName(hostCsprojPath)! , $"add {Path.GetFileName(hostCsprojPath)} package {name} -v {version}", ct);
            if (!success)
            {
                _logger.LogWarning("[ServerModuleManager] 安装 NuGet 包 {Package} 失败，可能引起编译错误", name);
            }
        }
    }

    public async Task UninstallNugetDependenciesAsync(string moduleJsonPath, string pluginShortName, string hostCsprojPath, string installModeModulesBaseDir, string srcSearchPath, CancellationToken ct)
    {
        if (!File.Exists(hostCsprojPath)) return;

        var deps = ParseNugetDependencies(moduleJsonPath);
        if (deps.Count == 0) return;

        // Collect what other plugins still need
        var otherPluginDeps = CollectAllPluginNugetDeps(installModeModulesBaseDir, srcSearchPath, excludePlugin: pluginShortName);

        foreach (var (name, _) in deps)
        {
            if (otherPluginDeps.Contains(name))
            {
                _logger.LogInformation("[ServerModuleManager] NuGet 包 {Package} 仍被其他活跃插件引用，触发保护，跳过卸载", name);
            }
            else
            {
                _logger.LogInformation("[ServerModuleManager] 触发 GC 回收，彻底卸载无主 NuGet 包: {Package}", name);
                await RunDotnetCommandAsync(Path.GetDirectoryName(hostCsprojPath)!, $"remove {Path.GetFileName(hostCsprojPath)} package {name}", ct);
            }
        }
    }

    private async Task<bool> RunDotnetCommandAsync(string workDir, string arguments, CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(workDir)) return false;

            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogWarning("[ServerModuleManager] 无法启动 dotnet 进程");
                return false;
            }

            await process.WaitForExitAsync(ct);
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("[ServerModuleManager] dotnet {Args} 执行成功:\n{Output}", arguments, output);
                return true;
            }
            else
            {
                _logger.LogWarning("[ServerModuleManager] dotnet {Args} 退出码: {Code}, 错误: {Error}", arguments, process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerModuleManager] 执行 dotnet {Args} 未知故障", arguments);
            return false;
        }
    }
}
