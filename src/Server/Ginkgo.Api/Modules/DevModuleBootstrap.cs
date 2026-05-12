using System.Reflection;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Ginkgo.Api.Modules;

public static class DevModuleBootstrap
{
    public sealed record DevLoaded(object Instance, AssemblyIsolatedLoadContext Alc, string BaseDirectory, ModuleManifest Manifest, Assembly Assembly);

    private static IEnumerable<string> ExpandRoots(IConfiguration config)
    {
        var roots = (config.GetSection("DevModules:ServerSearch").Get<string[]>() ?? Array.Empty<string>())
                    .DefaultIfEmpty(Path.Combine(Directory.GetCurrentDirectory(), "src", "Module"))
                    .ToArray();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> CandidatePaths(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;
            // 原样
            yield return raw;
            // 相对不同基准的组合
            yield return Path.GetFullPath(raw, Directory.GetCurrentDirectory());
            yield return Path.GetFullPath(raw, AppContext.BaseDirectory);
            // 向上递归查找 src/Module 目录（最多 6 层）
            var baseDirs = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (var b in baseDirs)
            {
                var cur = new DirectoryInfo(b);
                for (int i = 0; i < 6 && cur != null; i++)
                {
                    var probe = Path.Combine(cur.FullName, raw);
                    yield return probe;
                    var probe2 = Path.Combine(cur.FullName, "src", "Module");
                    yield return probe2;
                    cur = cur.Parent;
                }
            }
        }

        foreach (var r in roots)
        {
            foreach (var p in CandidatePaths(r))
            {
                try { if (Directory.Exists(p)) result.Add(Path.GetFullPath(p)); } catch { }
            }
        }

        // 记录解析出的根目录，便于排查
        try
        {
            Console.WriteLine("[DevModules] Candidate roots:");
            foreach (var r in result) Console.WriteLine("  - " + r);
        }
        catch { }
        return result;
    }

    public static List<DevLoaded> PreloadFromDevFolders(IConfiguration config)
    {
        var list = new List<DevLoaded>();
        try
        {
            var roots = ExpandRoots(config).ToList();
            Console.WriteLine($"[DevModules] Resolved {roots.Count} root(s)");

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) { Console.WriteLine($"[DevModules] Root not exists: {root}"); continue; }
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        Console.WriteLine($"Scanning directory: {dir}");
                        // 仅扫描服务端模块产物，避免误加载客户端 DLL（会导致文件被 API 进程锁定）
                        var dlls = Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories).ToList();
                        Console.WriteLine($"Found {dlls.Count} DLL files in {dir}");

                        // 仅匹配服务端产物（server/bin/**/Ginkgo.Module.*.dll），忽略 client 侧 DLL
                        // 优先选择 server/bin/ 根目录下的 DLL（打包产物），而非 bin/Debug/net8.0/ 下的构建产物
                        var serverBinDlls = dlls.Where(p =>
                                              Path.GetFileName(p).StartsWith("Ginkgo.Module.", StringComparison.OrdinalIgnoreCase) &&
                                              p.Contains(Path.Combine("server", "bin") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                              .ToList();
                        // 优先选择与目录名匹配的 DLL（解决模块引用其他模块时 bin 目录下存在多个 Ginkgo.Module.*.dll 的问题）
                        // 其次选择路径层级最浅的（即 server/bin/Xxx.dll 优先于 server/bin/Debug/net8.0/Xxx.dll）
                        var dirName = Path.GetFileName(dir); // e.g. "Ginkgo.Module.PluginStore"
                        var dll = serverBinDlls
                            .OrderByDescending(p => string.Equals(Path.GetFileNameWithoutExtension(p), dirName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                            .ThenBy(p => p.Split(Path.DirectorySeparatorChar).Length)
                            .FirstOrDefault();
                        if (dll == null)
                        {
                            // 记录非匹配项，便于排查
                            foreach (var cand in dlls)
                            {
                                if (Path.GetFileName(cand).StartsWith("Ginkgo.Module.", StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"  Found Ginkgo.Module DLL but path doesn't match: {cand}");
                                }
                            }
                            continue;
                        }

                        if (dll == null)
                        {
                            Console.WriteLine($"No matching DLL found in {dir}");
                            foreach (var d in dlls.Where(p => Path.GetFileName(p).StartsWith("Ginkgo.Module.", StringComparison.OrdinalIgnoreCase)))
                            {
                                Console.WriteLine($"  Found Ginkgo.Module DLL but path doesn't match: {d}");
                            }
                            continue;
                        }

                        Console.WriteLine($"Found matching DLL: {dll}");

                        var baseDir = Path.GetDirectoryName(dll)!;
                        var alc = new AssemblyIsolatedLoadContext($"dev_{Path.GetFileName(dir)}_{Ginkgo.Domain.Utils.SequentialGuid.NewGuid():N}", baseDir);
                        var asm = alc.LoadFromAssemblyPath(dll);
                        Console.WriteLine($"Loaded assembly: {asm.FullName}");


                        // 将该模块输出目录下的 XML 文档拷贝到 AppContext.BaseDirectory/modules/_dev/<module>
                        // 这样 Swagger 在启动时递归加载 BaseDirectory 下的 *.xml 即可拾取到开发期模块的注释
                        try
                        {
                            var xmlSourceDir = baseDir;
                            var xmlFiles = Directory.EnumerateFiles(xmlSourceDir, "*.xml", SearchOption.TopDirectoryOnly).ToList();
                            if (xmlFiles.Count > 0)
                            {
                                var xmlTargetDir = Path.Combine(AppContext.BaseDirectory, "modules", "_dev", Path.GetFileName(dir));
                                Directory.CreateDirectory(xmlTargetDir);
                                foreach (var xml in xmlFiles)
                                {
                                    try { File.Copy(xml, Path.Combine(xmlTargetDir, Path.GetFileName(xml)), true); } catch { }
                                }
                                try { Console.WriteLine($"[DevModules] Copied {xmlFiles.Count} XML(s) to {xmlTargetDir}"); } catch { }
                            }
                        }
                        catch { }

                        var serverModuleType = typeof(Ginkgo.Plugin.Abstractions.IServerModule);
                        Console.WriteLine($"Looking for implementations of: {serverModuleType.FullName} from {serverModuleType.Assembly.FullName}");

                        // 使用类型名称比较而不是类型引用比较，以解决程序集加载上下文问题
                        Type[] allTypes;
                        try
                        {
                            allTypes = asm.GetTypes();
                        }
                        catch (System.Reflection.ReflectionTypeLoadException rtle)
                        {
                            Console.WriteLine($"[DevModules] ReflectionTypeLoadException in {dll}:");
                            foreach (var le in rtle.LoaderExceptions ?? Array.Empty<Exception>())
                                Console.WriteLine($"  LoaderException: {le?.Message}");
                            allTypes = rtle.Types.Where(t => t != null).ToArray()!;
                        }
                        var moduleType = allTypes.FirstOrDefault(t =>
                            !t.IsInterface && !t.IsAbstract &&
                            t.GetInterfaces().Any(i => i.FullName == "Ginkgo.Plugin.Abstractions.IServerModule"));
                        if (moduleType == null)
                        {
                            Console.WriteLine($"No IServerModule implementation found in {dll}");
                            var types = asm.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract).ToList();
                            Console.WriteLine($"Available types: {string.Join(", ", types.Select(t => t.FullName))}");

                            // 检查是否有 CodeGeneratorServerModule 类型
                            var codeGenType = types.FirstOrDefault(t => t.Name == "CodeGeneratorServerModule");
                            if (codeGenType != null)
                            {
                                Console.WriteLine($"Found CodeGeneratorServerModule: {codeGenType.FullName}");
                                Console.WriteLine($"Interfaces: {string.Join(", ", codeGenType.GetInterfaces().Select(i => i.FullName))}");
                                Console.WriteLine($"Base types: {string.Join(", ", GetBaseTypes(codeGenType).Select(t => t.FullName))}");
                            }
                            continue;
                        }

                        Console.WriteLine($"Found module type: {moduleType.FullName}");

                        object module;
                        try
                        {
                            module = Activator.CreateInstance(moduleType)!;
                            Console.WriteLine($"Created module instance: {module.GetType().FullName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to create instance of {moduleType.FullName}: {ex.Message}");
                            Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                            continue;
                        }

                        var ver = asm.GetName().Version?.ToString() ?? "0.0.0-dev";
                        var manifest = new ModuleManifest
                        {
                            Id = Path.GetFileName(dir),
                            Name = Path.GetFileName(dir),
                            Version = ver,
                            HasClient = false,
                            Server = new ServerConfig { EntryAssembly = dll }
                        };

                        // 延后 Initialize 到 Program.cs 中处理（以使用真实的 builder.Services 与配置）
                        list.Add(new DevLoaded(module, alc, baseDir, manifest, asm));
                    }
                    catch { }
                }
            }
        }
        catch { }
        return list;
    }

    public static void RegisterMvcPartsAndNotify(ApplicationPartManager partManager, IEnumerable<DevLoaded> loaded, MvcActionDescriptorChangeProvider notifier)
    {
        foreach (var d in loaded)
        {
            try { partManager.ApplicationParts.Add(new AssemblyPart(d.Assembly)); } catch { }
        }
        try { notifier.NotifyChanges(); } catch { }
    }

    private static IEnumerable<Type> GetBaseTypes(Type type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }
    }
}


