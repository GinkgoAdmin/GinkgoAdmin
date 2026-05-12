using System.Reflection;
using System.Runtime.Loader;

namespace Ginkgo.Api.Modules;

public sealed class AssemblyIsolatedLoadContext : AssemblyLoadContext
{
    private readonly string _baseDirectory;

    public AssemblyIsolatedLoadContext(string name, string baseDirectory) : base(name, isCollectible: true)
    {
        _baseDirectory = baseDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            var name = assemblyName.Name ?? string.Empty;
            bool isExcluded = name.StartsWith("System.ClientModel", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("System.Memory.Data", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("System.Net.ServerSentEvents", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("Microsoft.Extensions.AI", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("OpenAI", StringComparison.OrdinalIgnoreCase) || 
                              name.StartsWith("Azure.", StringComparison.OrdinalIgnoreCase);

            // 委托公共框架程序集和共享第三方库给默认上下文，避免类型不一致（DI 解析失败的根因）
            if (!isExcluded &&
                (name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "netstandard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "SqlSugar", StringComparison.OrdinalIgnoreCase) || // 与宿主共享 SqlSugar
                string.Equals(name, "Ginkgo.Infrastructure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Ginkgo.Infrastructure.Abstractions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Ginkgo.Plugin.Abstractions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Ginkgo.Shared", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Ginkgo.Domain", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Ginkgo.ServerToolkit", StringComparison.OrdinalIgnoreCase)))
            {
                var sharedAssembly = Default.Assemblies.FirstOrDefault(a => 
                    string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                
                if (sharedAssembly != null)
                {
                    Console.WriteLine($"[ALC] ALC '{Name}' resolved '{assemblyName}' to Default shared: {sharedAssembly.FullName}");
                    return sharedAssembly;
                }

                // 如果默认上下文中尚未加载，尝试让 Default 不带版本号主动加载
                try 
                {
                    var loaded = Default.LoadFromAssemblyName(new AssemblyName(name));
                    Console.WriteLine($"[ALC] ALC '{Name}' actively loaded to Default: {loaded.FullName}");
                    return loaded;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ALC] ALC '{Name}' failed to actively load '{name}': {ex.Message}");
                }

                // 仅仅记录一下未能从宿主解析
                Console.WriteLine($"[ALC] ALC '{Name}' could not find shared '{name}' in Default Context, will fallback to local plugin directory.");
            }

            // 插件契约程序集（*.Contracts）需要跨模块共享同一个 Type，
            // 优先检查默认上下文是否已加载，未加载则从模块本地目录加载到默认上下文
            if (name.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase))
            {
                // 检查默认上下文是否已经加载了该程序集
                var existing = Default.Assemblies.FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    return existing;

                // 默认上下文未加载，从模块本地目录加载到默认上下文
                var contractsDll = Path.Combine(_baseDirectory, name + ".dll");
                if (File.Exists(contractsDll))
                {
                    return Default.LoadFromAssemblyPath(contractsDll);
                }

                return null; // 回退到默认解析
            }

            var candidate = Path.Combine(_baseDirectory, name + ".dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
        }
        catch { }
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var os = "win";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)) os = "linux";
        else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)) os = "osx";

        var runtimeId = $"{os}-{arch}";
        
        // 尝试从 runtimes/rid/native 加载
        var extensions = new[] { ".dll", ".so", ".dylib", "" };
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(_baseDirectory, "runtimes", runtimeId, "native", unmanagedDllName + ext);
            if (File.Exists(candidate))
            {
                return LoadUnmanagedDllFromPath(candidate);
            }
        }
        
        // 如果上面找不到，尝试直接从模块根目录加载
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(_baseDirectory, unmanagedDllName + ext);
            if (File.Exists(candidate))
            {
                return LoadUnmanagedDllFromPath(candidate);
            }
        }

        return base.LoadUnmanagedDll(unmanagedDllName);
    }
}










