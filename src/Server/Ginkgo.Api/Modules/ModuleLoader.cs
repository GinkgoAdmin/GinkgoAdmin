using System.IO.Compression;
using System.Reflection;
using Ginkgo.Plugin.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Ginkgo.Api.Modules;

public sealed class ModuleLoader
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly ModuleRuntimeManager? _runtime;
    private readonly ApplicationPartManager? _partManager;
    private readonly MvcActionDescriptorChangeProvider? _changeProvider;

    public ModuleLoader(IServiceCollection services, IConfiguration configuration, ModuleRuntimeManager? runtime = null, ApplicationPartManager? partManager = null, MvcActionDescriptorChangeProvider? changeProvider = null)
    {
        _services = services; _configuration = configuration; _runtime = runtime; _partManager = partManager; _changeProvider = changeProvider;
    }

    public bool TryLoadServerSide(string packagePath, ModuleManifest manifest, out string? error)
    {
        error = null;
        try
        {
            if (manifest.Server?.EntryAssembly == null)
            {
                error = "manifest.server.entryAssembly 缺失";
                return false;
            }
            var unpackDir = EnsureUnpackToAppModules(packagePath, manifest);
            if (!ModuleDatabaseCompatibility.ShouldLoadModule(manifest.Id, _configuration, unpackDir))
            {
                error = "当前数据库不支持该模块（需要 PostgreSQL）";
                return false;
            }
            var entry = Path.Combine(unpackDir, manifest.Server.EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(entry))
            {
                error = $"入口程序集不存在: {manifest.Server.EntryAssembly}";
                return false;
            }
            var alc = new AssemblyIsolatedLoadContext($"mod_{manifest.Id}_{manifest.Version}", Path.GetDirectoryName(entry)!);
            var asm = alc.LoadFromAssemblyPath(entry);
            // 使用类型名称比较而不是类型引用比较，以解决程序集加载上下文问题
            var moduleType = asm.GetTypes().FirstOrDefault(t =>
                !t.IsInterface && !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.FullName == "Ginkgo.Plugin.Abstractions.IServerModule"));
            if (moduleType == null)
            {
                error = "未找到 IServerModule 实现";
                return false;
            }
            var module = Activator.CreateInstance(moduleType)!;
            // 使用反射调用 Initialize 方法，避免类型转换问题
            var initializeMethod = module.GetType().GetMethod("Initialize");
            if (initializeMethod != null)
            {
                initializeMethod.Invoke(module, new object[] { _services, _configuration });
            }
            // 仅登记为“已知模块”，不在此处直接加入 MVC/完成 OnLoad；交由运行时管理器在启用时处理
            _runtime?.RegisterKnown(module, alc, Path.GetDirectoryName(entry)!, manifest, asm);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string EnsureUnpackToAppModules(string packagePath, ModuleManifest manifest)
    {
        var baseDir = AppContext.BaseDirectory;
        var targetDir = Path.Combine(baseDir, "modules", manifest.Id, manifest.Version);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
            // P1-3：解压前做 ZipSlip / zip-bomb 校验
            SafeZipExtractor.ExtractToDirectory(packagePath, targetDir, overwrite: false);
        }
        return targetDir;
    }
}
