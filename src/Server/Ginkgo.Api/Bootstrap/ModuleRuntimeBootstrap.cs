// 模块运行时注册：将预加载的模块注册到 RuntimeManager，
// 按数据库 Enabled 状态执行 TryLoad，并同步 InstalledModulesStore。

using System.Reflection;
using Ginkgo.Api.Modules;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Ginkgo.Infrastructure.Runtime;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Ginkgo.Api.Bootstrap;

/// <summary>
/// 模块运行时注册：RegisterKnown / TryLoad / InstalledModulesStore 同步。
/// </summary>
public static class ModuleRuntimeBootstrap
{
    /// <summary>
    /// 注册开发模块的 MVC 控制器部件并触发刷新。
    /// </summary>
    public static void RegisterDevModuleMvcParts(this WebApplication app, ModulePreloadResult preload)
    {
        if (preload.DevModules.Count == 0) return;
        try
        {
            var configuration = app.Services.GetRequiredService<IConfiguration>();
            var compatibleDevModules = preload.DevModules
                .Where(m => ModuleDatabaseCompatibility.ShouldLoadModule(m.Manifest.Id, configuration, m.BaseDirectory))
                .ToList();
            if (compatibleDevModules.Count == 0) return;

            var partManager = app.Services.GetRequiredService<ApplicationPartManager>();
            var notifier = app.Services.GetRequiredService<MvcActionDescriptorChangeProvider>();
            DevModuleBootstrap.RegisterMvcPartsAndNotify(partManager, compatibleDevModules, notifier);
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] Dev module MVC registration failed: {ex.Message}"); }
    }

    /// <summary>
    /// 将预加载模块注册到运行时管理器并按启用状态加载，同步 InstalledModulesStore。
    /// 包含数据库建表检查。
    /// </summary>
    public static void RegisterAndLoadModules(this WebApplication app, ModulePreloadResult preload)
    {
        using var scope = app.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // 确保数据库表存在
        DatabaseMaintenanceService.EnsureDatabaseAndTables(scope.ServiceProvider);

        // 将预加载模块注册到运行时管理器，以便后续 OnLoad 调用
        try
        {
            var runtime = scope.ServiceProvider.GetRequiredService<ModuleRuntimeManager>();
            // 读取模块安装状态（默认启用）。这里必须包含软删除记录，避免磁盘残留目录在重启时复活。
            var syncPolicy = ModuleStartupSyncPolicy.FromDatabase(Array.Empty<InstalledModuleEntity>());
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<SqlSugar.ISqlSugarClient>();
                var all = db.Queryable<InstalledModuleEntity>().ToList();
                syncPolicy = ModuleStartupSyncPolicy.FromDatabase(all);
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] Load module enabled map failed: {ex.Message}"); }

            // 注册为"已知模块"，并按数据库 Enabled 决定是否热加载
            try
            {
                var partManager = scope.ServiceProvider.GetRequiredService<ApplicationPartManager>();
                var notifier = scope.ServiceProvider.GetRequiredService<MvcActionDescriptorChangeProvider>();
                foreach (var m in preload.Modules)
                {
                    if (!syncPolicy.ShouldSynchronize(m.Manifest.Id))
                    {
                        Console.WriteLine($"[Modules] Skip deleted module: {m.Manifest.Id}");
                        continue;
                    }
                    if (!ModuleDatabaseCompatibility.ShouldLoadModule(m.Manifest.Id, configuration, m.BaseDirectory))
                    {
                        Console.WriteLine($"[Modules] Skip incompatible module: {m.Manifest.Id} (requires PostgreSQL)");
                        continue;
                    }

                    try
                    {
                        runtime.RegisterKnown(m.Instance, m.Alc, m.BaseDirectory, m.Manifest, m.Assembly);
                        Console.WriteLine($"[Modules] RegisterKnown: {m.Manifest.Id}");
                    }
                    catch (Exception ex) { Console.WriteLine($"[Modules] RegisterKnown FAILED {m.Manifest.Id}: {ex.Message}"); }
                }
                foreach (var m in preload.Modules)
                {
                    if (!syncPolicy.ShouldSynchronize(m.Manifest.Id))
                    {
                        Console.WriteLine($"[Modules] Module {m.Manifest.Id} is DELETED, skipping TryLoad");
                        continue;
                    }
                    if (!ModuleDatabaseCompatibility.ShouldLoadModule(m.Manifest.Id, configuration, m.BaseDirectory))
                    {
                        Console.WriteLine($"[Modules] Module {m.Manifest.Id} is incompatible with current database, skipping TryLoad");
                        continue;
                    }

                    var enabled = syncPolicy.ResolveEnabled(m.Manifest.Id);
                    Console.WriteLine($"[Modules] Module {m.Manifest.Id}: enabled={enabled}");
                    if (enabled)
                    {
                        try
                        {
                            var ok = runtime.TryLoad(m.Manifest.Id, partManager, notifier, scope.ServiceProvider, out var loadErr);
                            Console.WriteLine($"[Modules] TryLoad {m.Manifest.Id}: success={ok}, error={loadErr ?? "none"}");
                        }
                        catch (Exception ex) { Console.WriteLine($"[Modules] TryLoad FAILED {m.Manifest.Id}: {ex.Message}"); }
                    }
                    else
                    {
                        Console.WriteLine($"[Modules] Module {m.Manifest.Id} is DISABLED, skipping TryLoad");
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] Module MVC registration/load failed: {ex.Message}"); }

            // 同步 InstalledModulesStore
            var store = scope.ServiceProvider.GetRequiredService<InstalledModulesStore>();
            foreach (var m in preload.Modules)
            {
                if (!syncPolicy.ShouldSynchronize(m.Manifest.Id))
                {
                    store.Remove(m.Manifest.Id);
                    continue;
                }
                if (!ModuleDatabaseCompatibility.ShouldLoadModule(m.Manifest.Id, configuration, m.BaseDirectory))
                {
                    store.Remove(m.Manifest.Id);
                    continue;
                }

                var enabled = syncPolicy.ResolveEnabled(m.Manifest.Id);
                // 将已存在于 modules 目录的模块恢复到"已安装"列表，便于前端展示
                // 使用同步版本避免在using块中使用await
                store.AddOrUpdateAsync(new InstalledModule
                {
                    Id = m.Manifest.Id,
                    Name = m.Manifest.Name,
                    Version = m.Manifest.Version,
                    HasClient = m.Manifest.HasClient,
                    Enabled = enabled,
                    InstalledAtUtc = DateTime.Now,
                    Publisher = m.Manifest.Publisher,
                    Homepage = m.Manifest.Homepage
                }).GetAwaiter().GetResult();
            }

            // 自愈：开发环境下，若 src/Module/{id}/ 存在源码但解决方案未包含，则补充注册
            try
            {
                var env = scope.ServiceProvider.GetService<IWebHostEnvironment>();
                var solutionManager = scope.ServiceProvider.GetService<SolutionManager>();
                if (solutionManager != null && env != null && env.IsDevelopment())
                {
                    SyncInstalledModulesToSolution(solutionManager, preload);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[BOOT] Module solution sync failed: {ex.Message}"); }
        }
        catch (Exception ex) { Console.WriteLine($"[BOOT] Module runtime registration failed: {ex.Message}"); }
    }

    /// <summary>
    /// 开发环境自愈：把 src/Module/{id} 下已存在的源码项目补充到解决方案。
    /// </summary>
    private static void SyncInstalledModulesToSolution(SolutionManager solutionManager, ModulePreloadResult preload)
    {
        var devModuleRoot = LocateDevModuleRoot();
        if (devModuleRoot == null) return;

        // 端能力探测：仅当主框架包含 WPF 客户端及 WPF UI 项目时，才把模块的 client 端补充注册到解决方案。
        // 开源版主框架缺少 WPF 端，若强行加入插件 WPF 客户端项目会导致解决方案生成/编译报错。
        var hasWpfFramework = HasWpfFrameworkProjects(devModuleRoot);

        foreach (var m in preload.Modules)
        {
            var moduleDir = Path.Combine(devModuleRoot, m.Manifest.Id);
            if (!Directory.Exists(moduleDir)) continue;

            string? serverCsproj = null, clientCsproj = null, contractsCsproj = null;
            var serverDir = Path.Combine(moduleDir, "server");
            if (Directory.Exists(serverDir))
                serverCsproj = Directory.GetFiles(serverDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var clientDir = Path.Combine(moduleDir, "client");
            if (hasWpfFramework && Directory.Exists(clientDir))
                clientCsproj = Directory.GetFiles(clientDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var contractsDir = Path.Combine(moduleDir, "contracts");
            if (Directory.Exists(contractsDir))
                contractsCsproj = Directory.GetFiles(contractsDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (serverCsproj == null && clientCsproj == null && contractsCsproj == null) continue;

            try
            {
                var added = solutionManager.AddModuleToSolutionAsync(m.Manifest.Id, serverCsproj, clientCsproj, contractsCsproj).GetAwaiter().GetResult();
                if (added)
                    Console.WriteLine($"[BOOT] 已将已安装模块 {m.Manifest.Id} 补充注册到解决方案");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOT] 自愈注册模块到解决方案失败 {m.Manifest.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 检测主框架源码是否包含 WPF 客户端与 WPF UI 项目（src/Client/Ginkgo.Wpf 与 Ginkgo.UI）。
    /// devModuleRoot 为 src/Module 目录，往上两级即仓库根目录。
    /// </summary>
    private static bool HasWpfFrameworkProjects(string devModuleRoot)
    {
        try
        {
            var repoRoot = Path.GetFullPath(Path.Combine(devModuleRoot, "..", ".."));
            var clientBase = Path.Combine(repoRoot, "src", "Client");
            if (!Directory.Exists(clientBase)) return false;
            var wpfCsproj = Path.Combine(clientBase, "Ginkgo.Wpf", "Ginkgo.Wpf.csproj");
            var uiCsproj = Path.Combine(clientBase, "Ginkgo.UI", "Ginkgo.UI.csproj");
            return File.Exists(wpfCsproj) && File.Exists(uiCsproj);
        }
        catch
        {
            return false;
        }
    }

    private static string? LocateDevModuleRoot()
    {
        var searchBases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var baseDir in searchBases)
        {
            var current = new DirectoryInfo(baseDir);
            for (int i = 0; i < 8 && current != null; i++)
            {
                var candidate = Path.Combine(current.FullName, "src", "Module");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(current.FullName, "GinkgoAdmin.sln")))
                    return Path.GetFullPath(candidate);
                current = current.Parent;
            }
        }
        return null;
    }
}
