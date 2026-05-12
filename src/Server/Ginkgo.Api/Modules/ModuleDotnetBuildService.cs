using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 调用 `dotnet build` 编译模块项目。
/// 源码包安装时用于自动生成 server/bin/Debug/net8.0/{moduleId}.dll，
/// 供 ModuleHotReloader / DevModuleBootstrap 加载。
/// </summary>
public sealed class ModuleDotnetBuildService
{
    private readonly ILogger<ModuleDotnetBuildService> _logger;

    public ModuleDotnetBuildService(ILogger<ModuleDotnetBuildService> logger)
    {
        _logger = logger;
    }

    public sealed class BuildResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StdOut { get; set; } = string.Empty;
        public string StdErr { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
        public string? OutputDll { get; set; }
    }

    /// <summary>
    /// 编译指定 csproj（Debug 配置），返回产物 DLL 路径（若能找到）
    /// </summary>
    public async Task<BuildResult> BuildAsync(string csprojPath, CancellationToken ct = default)
    {
        var result = new BuildResult();
        if (string.IsNullOrWhiteSpace(csprojPath) || !File.Exists(csprojPath))
        {
            result.Message = $"csproj 不存在: {csprojPath}";
            return result;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(csprojPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("build");
            psi.ArgumentList.Add(csprojPath);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Debug");
            psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-v:minimal");
            psi.ArgumentList.Add("/p:GenerateFullPaths=true");

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutSb.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrSb.AppendLine(e.Data); };

            if (!proc.Start())
            {
                result.Message = "无法启动 dotnet build 进程";
                return result;
            }
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // 最多等待 10 分钟
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(10));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                result.Message = "dotnet build 超时或已取消";
                return result;
            }

            result.StdOut = stdoutSb.ToString();
            result.StdErr = stderrSb.ToString();
            result.Ok = proc.ExitCode == 0;
            result.Message = result.Ok ? "编译成功" : $"编译失败（ExitCode={proc.ExitCode}）";

            // 尝试定位产物 DLL
            if (result.Ok)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(csprojPath);
                var serverDir = Path.GetDirectoryName(csprojPath)!;
                var binDir = Path.Combine(serverDir, "bin");
                if (Directory.Exists(binDir))
                {
                    var candidates = Directory.GetFiles(binDir, assemblyName + ".dll", SearchOption.AllDirectories)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .ToList();
                    result.OutputDll = candidates.FirstOrDefault();
                }
            }
        }
        catch (Exception ex)
        {
            result.Message = "执行 dotnet build 失败: " + ex.Message;
            _logger.LogError(ex, "[ModuleDotnetBuildService] build {Csproj} failed", csprojPath);
        }
        finally
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
        }
        return result;
    }

    /// <summary>
    /// 将指定 csproj 发布到指定输出目录（用于「编译包」打包）。
    /// 等价于执行：dotnet publish &lt;csproj&gt; -c &lt;configuration&gt; -o &lt;outputDir&gt; --no-self-contained --nologo -v:minimal
    /// 产物包含该项目及其所有 ProjectReference / PackageReference 的 DLL、deps.json、runtimeconfig.json。
    /// 【低成本反编译防御】显式关闭 PDB / XML 文档生成，使编译包内不再携带调试符号和方法注释。
    /// </summary>
    /// <param name="csprojPath">项目文件绝对路径</param>
    /// <param name="outputDir">发布输出目录（会自动创建）</param>
    /// <param name="configuration">构建配置，默认 Release</param>
    /// <param name="ct">取消令牌</param>
    public async Task<BuildResult> PublishAsync(string csprojPath, string outputDir, string configuration = "Release", CancellationToken ct = default)
    {
        var result = new BuildResult();
        if (string.IsNullOrWhiteSpace(csprojPath) || !File.Exists(csprojPath))
        {
            result.Message = $"csproj 不存在: {csprojPath}";
            return result;
        }
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            result.Message = "outputDir 不能为空";
            return result;
        }

        try
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            result.Message = $"无法创建输出目录: {ex.Message}";
            return result;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(csprojPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("publish");
            psi.ArgumentList.Add(csprojPath);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(configuration);
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputDir);
            psi.ArgumentList.Add("--no-self-contained");
            psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-v:minimal");
            psi.ArgumentList.Add("/p:GenerateFullPaths=true");
            // 明确避免自包含：只要引用的 DLL，不带 .NET 运行时
            psi.ArgumentList.Add("/p:UseAppHost=false");
            // 【低成本反编译防御】禁用 PDB 与 XML 文档生成，使产物内不含调试符号与方法注释
            // - DebugType=none: 不生成任何调试信息（embedded / portable / pdbonly 一律关闭）
            // - DebugSymbols=false: 关闭调试符号（双保险，防止 SDK 默认行为被 props 覆盖）
            // - GenerateDocumentationFile=false: 关闭 XML 文档输出（覆盖 Ginkgo.Api 等项目中的 true 设置）
            // 注意：不影响 ALC 加载、反射、运行期堆栈（仅失去行号信息），对插件功能无副作用
            psi.ArgumentList.Add("/p:DebugType=none");
            psi.ArgumentList.Add("/p:DebugSymbols=false");
            psi.ArgumentList.Add("/p:GenerateDocumentationFile=false");

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutSb.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrSb.AppendLine(e.Data); };

            if (!proc.Start())
            {
                result.Message = "无法启动 dotnet publish 进程";
                return result;
            }
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // 最多等待 10 分钟
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(10));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                result.Message = "dotnet publish 超时或已取消";
                return result;
            }

            result.StdOut = stdoutSb.ToString();
            result.StdErr = stderrSb.ToString();
            result.Ok = proc.ExitCode == 0;
            result.Message = result.Ok ? "发布成功" : $"发布失败（ExitCode={proc.ExitCode}）";

            // 尝试定位主产物 DLL
            if (result.Ok)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(csprojPath);
                var mainDll = Path.Combine(outputDir, assemblyName + ".dll");
                if (File.Exists(mainDll))
                    result.OutputDll = mainDll;
            }
        }
        catch (Exception ex)
        {
            result.Message = "执行 dotnet publish 失败: " + ex.Message;
            _logger.LogError(ex, "[ModuleDotnetBuildService] publish {Csproj} failed", csprojPath);
        }
        finally
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
        }
        return result;
    }
}
