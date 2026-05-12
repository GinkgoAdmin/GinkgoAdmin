// 模块预加载：扫描 modules/ 目录与开发模块源码目录，
// 在 Build 前完成 ALC 隔离加载与 Initialize 调用。

using System.Reflection;
using System.Text.Json;
using Ginkgo.Api.Modules;
using Ginkgo.Infrastructure.Runtime;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 模块预加载结果（生产模块 + 开发模块）。
/// </summary>
public class ModulePreloadResult
{
    /// <summary>
    /// 所有已预加载的模块（生产 + 开发）
    /// </summary>
    public List<(object Instance, AssemblyIsolatedLoadContext Alc, string BaseDirectory, ModuleManifest Manifest, Assembly Assembly)> Modules { get; } = new();

    /// <summary>
    /// 开发模式加载的模块（用于 MVC 控制器注册）
    /// </summary>
    public List<DevModuleBootstrap.DevLoaded> DevModules { get; } = new();
}

/// <summary>
/// 模块预加载：扫描已安装模块与开发模块，在 Build 前完成 ALC 隔离加载。
/// </summary>
public static class ModulePreloader
{
    /// <summary>
    /// 预加载所有已安装的服务端模块（生产 + 开发）。
    /// 安装模式下直接返回空结果。
    /// </summary>
    public static ModulePreloadResult PreloadModules(this WebApplicationBuilder builder, bool installationMode)
    {
        var result = new ModulePreloadResult();
        if (installationMode) return result;

        // 1. 生产模块扫描
        ScanProductionModules(builder, result);

        // 2. 开发模块扫描
        ScanDevModules(builder, result);

        return result;
    }

    /// <summary>
    /// 扫描 modules/ 目录，加载已发布的模块。
    /// </summary>
    private static void ScanProductionModules(WebApplicationBuilder builder, ModulePreloadResult result)
    {
        try
        {
            // Search for modules in multiple candidate directories
            var moduleCandidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "modules"),
                Path.Combine(builder.Environment.ContentRootPath, "modules")
            };
            // Deduplicate (in case they point to the same directory)
            var searchedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var modulesRoot in moduleCandidates)
            {
                var fullPath = Path.GetFullPath(modulesRoot);
                if (!searchedRoots.Add(fullPath)) continue;

                Console.WriteLine($"[Modules] Scanning: {fullPath} (exists: {Directory.Exists(fullPath)})");
                if (!Directory.Exists(fullPath)) continue;

                foreach (var manifestPath in Directory.EnumerateFiles(fullPath, "module.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        Console.WriteLine($"[Modules] Found manifest: {manifestPath}");
                        var manifest = JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllText(manifestPath));
                        if (manifest?.Server?.EntryAssembly == null)
                        {
                            Console.WriteLine($"[Modules]   Skipped: no server.entryAssembly in manifest");
                            continue;
                        }
                        var baseDir = Path.GetDirectoryName(manifestPath)!;
                        var entry = Path.Combine(baseDir, manifest.Server.EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
                        Console.WriteLine($"[Modules]   Entry DLL: {entry} (exists: {File.Exists(entry)})");
                        if (!File.Exists(entry))
                        {
                            Console.WriteLine($"[Modules]   Skipped: entry DLL not found");
                            continue;
                        }
                        var alc = new AssemblyIsolatedLoadContext($"pre_mod_{manifest.Id}_{manifest.Version}", Path.GetDirectoryName(entry)!);
                        var asm = alc.LoadFromAssemblyPath(entry);
                        Console.WriteLine($"[Modules]   Assembly loaded: {asm.FullName}");
                        var moduleType = asm.GetTypes().FirstOrDefault(t =>
                            !t.IsInterface && !t.IsAbstract &&
                            t.GetInterfaces().Any(i => i.FullName == "Ginkgo.Plugin.Abstractions.IServerModule"));
                        if (moduleType == null)
                        {
                            Console.WriteLine($"[Modules]   Skipped: no IServerModule implementation found");
                            continue;
                        }
                        Console.WriteLine($"[Modules]   Module type: {moduleType.FullName}");
                        var module = Activator.CreateInstance(moduleType)!;
                        var initializeMethod = module.GetType().GetMethod("Initialize");
                        if (initializeMethod != null)
                        {
                            initializeMethod.Invoke(module, new object[] { builder.Services, builder.Configuration });
                            Console.WriteLine($"[Modules]   Initialize() called successfully");
                        }
                        result.Modules.Add((module, alc, baseDir, manifest, asm));
                        Console.WriteLine($"[Modules]   Module registered: {manifest.Id} v{manifest.Version}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Modules]   ERROR loading module from {manifestPath}: {ex.Message}");
                        if (ex.InnerException != null)
                            Console.WriteLine($"[Modules]   Inner: {ex.InnerException.Message}");
                    }
                }
            }
            Console.WriteLine($"[Modules] Total preloaded: {result.Modules.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Modules] FATAL error scanning modules: {ex.Message}");
        }
    }

    /// <summary>
    /// 开发期从 src/Module/** 预加载模块程序集（支持生产通过配置开启）。
    /// </summary>
    private static void ScanDevModules(WebApplicationBuilder builder, ModulePreloadResult result)
    {
        try
        {
            if (builder.Environment.IsDevelopment() || string.Equals(builder.Configuration["DevModules:EnableServer"], "true", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Loading dev modules...");
                var devLoaded = DevModuleBootstrap.PreloadFromDevFolders(builder.Configuration);
                result.DevModules.AddRange(devLoaded);
                Console.WriteLine($"Found {devLoaded.Count} dev modules");
                // 在 Build 前执行 Initialize 需要使用真实的 builder.Services
                foreach (var d in devLoaded)
                {
                    try
                    {
                        Console.WriteLine($"Initializing module: {d.Manifest.Id}");
                        // 使用反射调用 Initialize 方法，避免类型转换问题
                        var initializeMethod = d.Instance.GetType().GetMethod("Initialize");
                        if (initializeMethod != null)
                        {
                            // 通过 DebuggerHidden 辅助方法调用，避免 VS 调试器在模块内部的
                            // 第一机会异常（如 ALC 加载、第三方库内部 catch 住的 NRE）处中断
                            ModuleInitHelper.SafeInvokeInitialize(initializeMethod, d.Instance, builder.Services, builder.Configuration, d.Manifest.Id);
                            result.Modules.Add((d.Instance, d.Alc, d.BaseDirectory, d.Manifest, d.Assembly));
                        }
                        else
                        {
                            Console.WriteLine($"Module {d.Manifest.Id} does not have Initialize method");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to initialize module {d.Manifest.Id}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load dev modules: {ex.Message}");
        }
    }
}

/// <summary>
/// 模块初始化辅助类 - 使用 DebuggerHidden 防止 VS 调试器在模块内部的
/// 第一机会异常（如 ALC 加载、第三方库内部正常的 try-catch 流程）处中断。
/// </summary>
static class ModuleInitHelper
{
    /// <summary>
    /// 安全调用模块的 Initialize 方法。
    /// [DebuggerHidden] 使得 VS2022 即使勾选了"引发此异常类型时中断"，
    /// 也不会在此方法内部的第一机会异常处暂停调试。
    /// </summary>
    [System.Diagnostics.DebuggerHidden]
    public static void SafeInvokeInitialize(
        System.Reflection.MethodInfo initializeMethod,
        object moduleInstance,
        IServiceCollection services,
        IConfiguration configuration,
        string moduleId)
    {
        try
        {
            initializeMethod.Invoke(moduleInstance, new object[] { services, configuration });
        }
        catch (System.Reflection.TargetInvocationException tie)
        {
            Console.WriteLine($"[CRITICAL ERROR] TargetInvocationException in {moduleId}: {tie.InnerException}");
            throw;
        }
    }
}
