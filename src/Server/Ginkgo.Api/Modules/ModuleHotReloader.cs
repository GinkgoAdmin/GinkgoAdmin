using System.Text.Json;
using Ginkgo.Domain.Modules;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// Orchestrates runtime hot enable/disable/uninstall for server modules.
/// Uses ApplicationPartManager + MvcActionDescriptorChangeProvider for controller (route) add/remove,
/// and ModuleRuntimeManager for ALC lifecycle and module callbacks.
/// </summary>
public sealed class ModuleHotReloader
{
    private readonly ModuleRuntimeManager _runtime;
    private readonly ApplicationPartManager _parts;
    private readonly MvcActionDescriptorChangeProvider _changeProvider;
    private readonly IServiceProvider _sp;
    private readonly InstalledModulesStore _store;
    private readonly ModuleInstaller _installer;
    private readonly ModuleSqlExecutor _sql;
    private readonly ClientTaskService _clientTasks;

    private readonly ILogger<ModuleHotReloader> _logger;

    public ModuleHotReloader(
        ModuleRuntimeManager runtime,
        ApplicationPartManager parts,
        MvcActionDescriptorChangeProvider changeProvider,
        IServiceProvider sp,
        InstalledModulesStore store,
        ModuleInstaller installer,
        ModuleSqlExecutor sql,
        ClientTaskService clientTasks,
        ILogger<ModuleHotReloader> logger)
    {
        _runtime = runtime; _parts = parts; _changeProvider = changeProvider; _sp = sp; _store = store; _installer = installer; _sql = sql; _clientTasks = clientTasks; _logger = logger;
    }

    public async Task<bool> EnableAsync(string moduleId, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 1) 读取已安装信息或磁盘 manifest
            var (manifest, entryPath) = ResolveManifestAndEntry(moduleId);
            if (manifest == null || entryPath == null)
            {
                _logger.LogWarning("[ModuleHotReloader] Enable: manifest not found for {Module}", moduleId);
                return false;
            }

            // 2) 若运行时未知该模块，则创建并注册（不执行 OnLoad）
            if (!_runtime.IsLoaded(moduleId))
            {
                if (!TryEnsureKnown(moduleId, entryPath))
                {
                    _logger.LogError("[ModuleHotReloader] Enable: failed to register known for {Module}", moduleId);
                    return false;
                }
            }

            // 3) 加入 MVC 并触发 OnLoad
            if (!_runtime.TryLoad(moduleId, _parts, _changeProvider, _sp, out var err))
            {
                _logger.LogError("[ModuleHotReloader] Enable: TryLoad failed for {Module}: {Error}", moduleId, err);
                // 回滚 DB 状态
                await TouchEnabledFlagAsync(moduleId, false, ct);
                return false;
            }

            // 4) 菜单可见（如有 RootCode）
            try
            {
                var rootCode = TryGetRootMenuCode(moduleId);
                if (!string.IsNullOrWhiteSpace(rootCode)) await _sql.SetMenuTreeVisibleAsync(rootCode!, true, ct);
            }
            catch (Exception mex) { _logger.LogWarning(mex, "[ModuleHotReloader] Enable: SetMenuTreeVisible failed for {Module}", moduleId); }

            // 5) 更新数据库 Enabled=true
            await TouchEnabledFlagAsync(moduleId, true, ct);

            // 6) 通知客户端刷新（广播）
            try { _clientTasks.EnqueueBroadcast(moduleId, manifest.Version ?? string.Empty, "module:stateChanged"); }
            catch (Exception ex) { _logger.LogWarning(ex, "[ModuleHotReloader] Enable: broadcast failed for {Module}", moduleId); }

            _logger.LogInformation("[ModuleHotReloader] Enable OK: {Module} in {Elapsed} ms", moduleId, sw.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModuleHotReloader] Enable exception for {Module}", moduleId);
            await TouchEnabledFlagAsync(moduleId, false, ct);
            return false;
        }
        finally { sw.Stop(); }
    }

    public async Task<bool> DisableAsync(string moduleId, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 1) OnUnload + 移除 MVC + 卸载 ALC
            if (!_runtime.TryUnload(moduleId, _parts, _changeProvider, out var err))
            {
                _logger.LogWarning("[ModuleHotReloader] Disable: TryUnload failed for {Module}: {Error}", moduleId, err);
                // 标记异常状态：仅记录日志，不抛异常，避免影响其他模块
            }

            // Ensure ALC is collectible and file locks are released (Windows)
            try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch { /* swallow: best-effort GC */ }

            // 2) 更新数据库 Enabled=false
            await TouchEnabledFlagAsync(moduleId, false, ct);

            // 3) 菜单禁用（根据 install.json RootCode 将整个子树置为不可见），随后广播客户端刷新
            try
            {

                var rootCode = TryGetRootMenuCode(moduleId);
                if (!string.IsNullOrWhiteSpace(rootCode)) await _sql.SetMenuTreeVisibleAsync(rootCode!, false, ct);
                // 4) 通知客户端刷新（广播）
                try { var (man, _) = ResolveManifestAndEntry(moduleId); _clientTasks.EnqueueBroadcast(moduleId, man?.Version ?? string.Empty, "module:stateChanged"); } catch (Exception bex) { _logger.LogWarning(bex, "[ModuleHotReloader] Disable: broadcast failed for {Module}", moduleId); }
            }
            catch (Exception mex) { _logger.LogWarning(mex, "[ModuleHotReloader] Disable: SetMenuTreeVisible failed for {Module}", moduleId); }

            _logger.LogInformation("[ModuleHotReloader] Disable OK: {Module} in {Elapsed} ms", moduleId, sw.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModuleHotReloader] Disable exception for {Module}", moduleId);
            return false;
        }
        finally { sw.Stop(); }
    }

    public async Task<ModuleInstaller.ModuleOperationResult> UninstallAsync(string moduleId, CancellationToken ct = default)
    {
        // 先尝试热卸载，再执行卸载流程（SQL/菜单/记录迁移）
        try { await DisableAsync(moduleId, ct); } catch (Exception ex) { _logger.LogWarning(ex, "[ModuleHotReloader] Uninstall: pre-disable failed for {Module}", moduleId); }

        // 缓存卸载前的版本号用于客户端通知
        var (manifest, _) = ResolveManifestAndEntry(moduleId);
        var ver = manifest?.Version ?? string.Empty;

        var res = await _installer.UninstallAsync(moduleId, ct);
        if (res.Ok)
        {
            try { _clientTasks.EnqueueBroadcast(moduleId, ver, "module:stateChanged"); } catch (Exception ex) { _logger.LogWarning(ex, "[ModuleHotReloader] Uninstall: broadcast failed for {Module}", moduleId); }
        }
        return res;
    }

    private bool TryEnsureKnown(string moduleId, string entryAssemblyPath)
    {
        // 若 runtime 尚未记录，尝试从磁盘创建并登记
        if (!_runtime.IsLoaded(moduleId))
        {
            return _runtime.TryCreateAndRegisterFromPath(moduleId, entryAssemblyPath, out _);
        }
        return true;
    }

    private async Task TouchEnabledFlagAsync(string moduleId, bool enabled, CancellationToken ct)
    {
        var list = _store.List().ToList();
        var exists = list.FirstOrDefault(x => string.Equals(x.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        if (exists != null)
        {
            exists.Enabled = enabled;
            await _store.AddOrUpdateAsync(exists);
        }
    }

    private static (ModuleManifest? manifest, string? entryPath) ResolveManifestAndEntry(string moduleId)
    {
        try
        {
            // 1) Packaged modules under bin/.../modules/{moduleId}/{version}
            var baseRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
            if (Directory.Exists(baseRoot))
            {
                var verDir = Directory.EnumerateDirectories(baseRoot).OrderByDescending(d => d).FirstOrDefault();
                if (verDir != null)
                {
                    var manifestPath = Path.Combine(verDir, "module.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifest = JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllText(manifestPath));
                        if (manifest?.Server?.EntryAssembly != null)
                        {
                            var entry = Path.Combine(verDir, manifest.Server.EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(entry)) return (manifest, entry);
                            // If EntryAssembly is absolute, Path.Combine returns it; still verify
                            if (Path.IsPathRooted(manifest.Server.EntryAssembly) && File.Exists(manifest.Server.EntryAssembly))
                                return (manifest, manifest.Server.EntryAssembly);
                        }
                    }
                }
            }

            // 2) Dev fallback: look for dll under src/Module/{moduleId}/server/bin
            //    Go up from AppContext.BaseDirectory to repo's src folder
            var devServerDirCandidates = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..","..","..","Module", moduleId, "server")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..","..","..","..","Module", moduleId, "server"))
            };
            foreach (var devServerDir in devServerDirCandidates)
            {
                try
                {
                    if (!Directory.Exists(devServerDir)) continue;
                    var devBin = Path.Combine(devServerDir, "bin");
                    if (!Directory.Exists(devBin)) continue;
                    var dllName = moduleId + ".dll";
                    // 优先选择 server/bin/ 根目录下的 DLL（打包产物），而非 bin/Debug/net8.0/ 下的构建产物
                    var allEntries = Directory.EnumerateFiles(devBin, dllName, SearchOption.AllDirectories).ToList();
                    var entry = allEntries
                        .OrderBy(p => Path.GetDirectoryName(p)!.Equals(devBin, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                        .FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(entry) || !File.Exists(entry)) continue;

                    ModuleManifest? manifest = null;
                    var devManifestPath = Path.Combine(devServerDir, "module.json");
                    if (File.Exists(devManifestPath))
                    {
                        try { manifest = JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllText(devManifestPath)); }
                        catch { manifest = null; }
                    }
                    manifest ??= new ModuleManifest
                    {
                        Id = moduleId,
                        Name = moduleId,
                        Version = "dev",
                        HasClient = false,
                        Server = new ServerConfig { EntryAssembly = entry }
                    };
                    return (manifest, entry);
                }
                catch (Exception ex) { Console.WriteLine($"[ModuleHotReloader] Dev resolve error: {ex.Message}"); }
            }

            return (null, null);
        }
        catch { return (null, null); }
    }
    /// <summary>
    /// 查找模块的菜单根 Code（支持开发和生产环境）
    /// </summary>
    private string? TryGetRootMenuCode(string moduleId)
    {
        try
        {
            // 使用 ModuleInstaller 统一的 install.json 查找逻辑（支持开发和生产路径）
            var installPath = _installer.FindInstallJsonPathPublic(moduleId);
            if (installPath == null || !File.Exists(installPath)) return null;
            var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
            return spec?.Menus?.RootCode;
        }
        catch { return null; }
    }

}

