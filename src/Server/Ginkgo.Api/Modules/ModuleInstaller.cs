using System.Collections.Concurrent;
using Ginkgo.Api.Auth;
using Ginkgo.Application.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

public sealed class ModuleInstaller
{
    private readonly InstalledModulesStore _store;
    private readonly ModuleLoader _loader;
    private readonly InstalledModulesStore _installed;
    private readonly ModuleSqlExecutor _sql;
    private readonly SolutionManager? _solutionManager;
    private readonly WebModuleManager? _webModuleManager;
    private readonly PendingDeleteManager? _pendingDelete;
    private readonly ServerModuleManager? _serverModuleManager;
    private readonly PermissionCacheInvalidator? _permissionCacheInvalidator;
    private readonly IConfiguration _config;
    private readonly ILogger<ModuleInstaller> _logger;
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// P1-5：按 moduleId 的串行化信号量。Install / Upgrade / Uninstall 都从这里取锁，
    /// 避免并发安装/卸载同一模块时互相踩 install.json 解析、菜单注册、目录删除等竞态。
    /// 不同模块互不阻塞；同一模块的并发请求按到达顺序排队。
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _moduleLocks = new(StringComparer.OrdinalIgnoreCase);

    private static SemaphoreSlim GetModuleLock(string moduleId)
        => _moduleLocks.GetOrAdd(moduleId, _ => new SemaphoreSlim(1, 1));

    public ModuleInstaller(InstalledModulesStore store, ModuleLoader loader, ModuleSqlExecutor sql, IConfiguration config, ILogger<ModuleInstaller> logger, IServiceScopeFactory scopes, SolutionManager? solutionManager = null, WebModuleManager? webModuleManager = null, ServerModuleManager? serverModuleManager = null, PendingDeleteManager? pendingDelete = null, PermissionCacheInvalidator? permissionCacheInvalidator = null)
    {
        _store = store; _installed = store; _loader = loader; _sql = sql; _config = config; _logger = logger; _scopes = scopes; _solutionManager = solutionManager; _webModuleManager = webModuleManager; _serverModuleManager = serverModuleManager; _pendingDelete = pendingDelete; _permissionCacheInvalidator = permissionCacheInvalidator;
    }

    public sealed record ModuleOperationResult(bool Ok, string Message, List<string>? PendingDeleteDirs = null);

    public async Task<ModuleOperationResult> InstallAsync(ModuleRepoItem repoItem, CancellationToken ct)
    {
        var gate = GetModuleLock(repoItem.Manifest.Id);
        await gate.WaitAsync(ct);
        try
        {
            return await InstallCoreAsync(repoItem, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ModuleOperationResult> InstallCoreAsync(ModuleRepoItem repoItem, CancellationToken ct)
    {
        // 安装前依赖检查
        var depCheck = CheckDependencies(repoItem.Manifest);
        if (!depCheck.Ok)
            return depCheck;

        bool ok = true; string msg = "安装成功";
        if (repoItem.Manifest.Server?.EntryAssembly != null)
        {
            if (!_loader.TryLoadServerSide(repoItem.PackagePath, repoItem.Manifest, out var err))
            {
                ok = false; msg = $"安装失败：{err}";
            }
        }
        string? menuRootCode = null;
        try
        {
            var baseDir = Path.GetDirectoryName(repoItem.PackagePath)!;
            var installPath = Path.Combine(baseDir, "install.json");
            if (File.Exists(installPath))
            {
                var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
                // 提取菜单根编码，用于后续持久化
                menuRootCode = spec?.Menus?.RootCode;
                if (spec?.SqlScripts != null || spec?.SqlScriptsByDialect != null)
                {
                    var installJsonDir = Path.GetDirectoryName(installPath)!;
                    var resolved = ModuleInstallScriptResolver.Resolve(installJsonDir, _config["Database:Provider"], spec);
                    await _sql.ExecuteScriptsAsync(resolved.AbsolutePaths, ct, resolved.ScriptsAreNativeDialect);
                }
                // 应用 install.json 中的 Menus 规则（如有），同时把模块 Id 写入 ginkgo_Sys_Menu.Module
                await _sql.ApplyMenusAsync(spec, repoItem.Manifest.Name ?? repoItem.Manifest.Id, repoItem.Manifest.Id, ct);
                // 应用 install.json 中的 ClientMenus 规则（如有），把客户端入口项写入共享菜单表并归属到本模块（Module=moduleId）
                // 与上方 ApplyMenusAsync 使用相同的 moduleId，保持模块归属一致；置于同一 try 块内，异常将传播到下方 catch 形成安装失败结果
                await _sql.ApplyClientMenusAsync(spec, repoItem.Manifest.Id, ct);
            }

            // 数据库存储模式：安装时将 config/*.sample 默认值写入 ginkgo_Sys_Settings
            await SeedModuleConfigToDatabaseAsync(repoItem.Manifest, baseDir, ct);
        }
        catch (Exception ex)
        {
            ok = false; msg = $"安装SQL执行失败：{ex.Message}";
        }
        await _store.AddOrUpdateAsync(new InstalledModule
        {
            Id = repoItem.Manifest.Id,
            Name = repoItem.Manifest.Name ?? repoItem.Manifest.Id,
            Version = repoItem.Manifest.Version,
            HasClient = repoItem.Manifest.HasClient,
            Enabled = true,
            InstalledAtUtc = DateTime.Now,
            Publisher = repoItem.Manifest.Publisher,
            Homepage = repoItem.Manifest.Homepage,
            MenuRootCode = menuRootCode
        });
        _permissionCacheInvalidator?.InvalidateAll();
        return new ModuleOperationResult(ok, msg);
    }

    public Task<ModuleOperationResult> UpgradeAsync(ModuleRepoItem repoItem, CancellationToken ct) => InstallAsync(repoItem, ct);

    public async Task<ModuleOperationResult> UninstallAsync(string moduleId, CancellationToken ct)
    {
        var gate = GetModuleLock(moduleId);
        await gate.WaitAsync(ct);
        try
        {
            return await UninstallCoreAsync(moduleId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ModuleOperationResult> UninstallCoreAsync(string moduleId, CancellationToken ct)
    {
        _logger.LogInformation("[UninstallAsync] 开始卸载模块 {ModuleId}", moduleId);

        // DAG 拓扑检查：防止强行卸载导致级联雪崩
        var blockDependents = await CheckTopologyForUninstallAsync(moduleId, ct);
        if (blockDependents.Count > 0)
        {
            var dependentNames = string.Join(", ", blockDependents);
            _logger.LogWarning("[UninstallAsync] 卸载被拦截：模块 {ModuleId} 当前仍被以下激活的插件所依赖：{DependentNames}", moduleId, dependentNames);
            return new ModuleOperationResult(false, $"卸载被拒绝：此插件仍被 [{dependentNames}] 依赖。请先卸载上级插件。");
        }

        // ★ 在移除存储记录之前，先读取已安装模块信息（含 MenuRootCode），供后续菜单移除兜底使用
        var installedModule = _installed.List().FirstOrDefault(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        var savedMenuRootCode = installedModule?.MenuRootCode;
        _logger.LogInformation("[UninstallAsync] 已安装模块信息: MenuRootCode={RootCode}", savedMenuRootCode ?? "(无)");

        // ★ 第一步：在移除任何文件之前，先移除关联的所有菜单
        var menusRemoved = false;
        try
        {
            // 查找 install.json 的路径（支持开发环境和生产环境）
            var installPath = FindInstallJsonPath(moduleId);
            _logger.LogInformation("[UninstallAsync] install.json 路径: {Path}", installPath ?? "(未找到)");
            
            if (installPath != null && File.Exists(installPath))
            {
                var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
                if (spec != null)
                {
                    _logger.LogInformation("[UninstallAsync] 读取到 install.json, RootCode: {RootCode}, 菜单项数: {Count}", 
                        spec.Menus?.RootCode ?? "(无)", spec.Menus?.Items?.Count ?? 0);
                    
                    // 通过 install.json 清理后台 RBAC 菜单
                    await _sql.RemoveMenusAsync(spec, ct);
                    menusRemoved = true;
                    _logger.LogInformation("[UninstallAsync] 已通过 install.json 删除后台菜单");

                    // 若 install.json 声明了 ClientMenus，同步清理多客户端入口（MenuGroupItem）
                    if (spec.ClientMenus != null && spec.ClientMenus.Count > 0)
                    {
                        await _sql.RemoveClientMenusByModuleAsync(moduleId, ct);
                        _logger.LogInformation("[UninstallAsync] 已按 install.json ClientMenus 配置清理多客户端入口（Module={ModuleId}）", moduleId);
                    }
                    
                    // 执行卸载 SQL 脚本
                    if (spec.UninstallSql != null && spec.UninstallSql.Length > 0)
                    {
                        _logger.LogInformation("[UninstallAsync] 保留插件业务数据，跳过 uninstall.sql");
                    }
                }
            }
            else
            {
                _logger.LogWarning("[UninstallAsync] 未找到 install.json，跳过 SQL 清理");
            }

            // 兜底：若 install.json 未能移除菜单，但已持久化 MenuRootCode，则通过 RootCode 移除整棵菜单树
            if (!menusRemoved && !string.IsNullOrWhiteSpace(savedMenuRootCode))
            {
                _logger.LogInformation("[UninstallAsync] 使用已持久化的 MenuRootCode={RootCode} 兜底移除菜单", savedMenuRootCode);
                await _sql.RemoveMenusByRootCodeAsync(savedMenuRootCode, ct);
                menusRemoved = true;
                _logger.LogInformation("[UninstallAsync] 已通过 MenuRootCode 兜底删除菜单");
            }

            // ★ 终极兜底：按 Module 字段清理插件在主框架共享表（菜单/字典/字典项/配置）中残留的所有记录
            //    这样即便 install.json 丢失、MenuRootCode 未持久化，只要插件安装时正确填入 Module=ModuleId，
            //    依然可以在卸载阶段彻底回收，主框架的菜单/字典/配置不会留下脏数据。
            try
            {
                await _sql.RemoveModuleDataAsync(moduleId, ct);
                _logger.LogInformation("[UninstallAsync] 已按 Module={ModuleId} 清理共享菜单/字典/配置中的插件数据", moduleId);
            }
            catch (Exception cleanEx)
            {
                _logger.LogWarning(cleanEx, "[UninstallAsync] 按 Module 清理共享数据失败：{Msg}", cleanEx.Message);
            }

            // ★ 同阶段清理：按 Module=moduleId 移除本模块写入共享菜单表的客户端入口项（ClientMenus）及其授权关联，
            //    仅删除归属本模块的入口项，不触碰 Module='sys' 项、不删除 MenuGroup（需求 7.1/7.2）。
            //    与 RemoveModuleDataAsync 保持相同的容错语义：失败仅记录告警，不中断后续卸载与目录清理流程。
            try
            {
                await _sql.RemoveClientMenusByModuleAsync(moduleId, ct);
                _logger.LogInformation("[UninstallAsync] 已按 Module={ModuleId} 清理共享菜单表中的客户端入口项(ClientMenus)", moduleId);
            }
            catch (Exception cleanEx)
            {
                _logger.LogWarning(cleanEx, "[UninstallAsync] 按 Module 清理客户端入口项失败：{Msg}", cleanEx.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UninstallAsync] 菜单移除或卸载 SQL 执行异常: {Message}", ex.Message);
        }

        // ★ 第二步：菜单已移除后，再从已安装列表中移除模块记录
        _installed.Remove(moduleId);
        _permissionCacheInvalidator?.InvalidateAll();

        // 从解决方案移除模块项目
        if (_solutionManager != null)
        {
            try
            {
                await _solutionManager.RemoveModuleFromSolutionAsync(moduleId, ct);
            }
            catch { }
        }

        // 卸载 Web 前端文件
        if (_webModuleManager != null)
        {
            try
            {
                await _webModuleManager.UninstallWebFilesAsync(moduleId, ct);
            }
            catch { }
        }

        // 卸载后端 NuGet 依赖（GC回收）
        if (_serverModuleManager != null)
        {
            try
            {
                var installPath = FindInstallJsonPath(moduleId);
                var moduleJsonPath = installPath != null ? Path.Combine(Path.GetDirectoryName(installPath)!, "module.json") : null;
                if (moduleJsonPath != null && File.Exists(moduleJsonPath))
                {
                    var searchPaths = _config.GetSection("DevModules:ServerSearch").Get<string[]>()?.FirstOrDefault() ?? "src/Module";
                    var hostCsprojPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Server", "Ginkgo.Api", "Ginkgo.Api.csproj"));
                    await _serverModuleManager.UninstallNugetDependenciesAsync(moduleJsonPath, moduleId, hostCsprojPath, Path.Combine(AppContext.BaseDirectory, "modules"), searchPaths, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[UninstallAsync] NuGet 垃圾回收失败: {Message}", ex.Message);
            }
        }

        // 强制 GC 回收，释放 DLL 文件句柄
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            // 等待一小段时间让文件句柄完全释放
            await Task.Delay(200, ct);
        }
        catch { }

        var pendingDirs = new List<string>();

        await DeleteUniappPluginFilesAsync(moduleId, pendingDirs, ct);

        // 删除模块源文件目录 (src/Module/{moduleId})
        var moduleSourceDir = FindModuleSourceDir(moduleId);
        if (moduleSourceDir != null && Directory.Exists(moduleSourceDir))
        {
            if (!await TryDeleteDirectoryAsync(moduleSourceDir, ct))
            {
                pendingDirs.Add(moduleSourceDir);
                _logger.LogWarning("[UninstallAsync] 模块源文件目录被锁定，已加入待删除队列: {Dir}", moduleSourceDir);
            }
            else
            {
                _logger.LogInformation("[UninstallAsync] 已删除模块源文件目录 {Dir}", moduleSourceDir);
            }
        }

        // 删除已安装模块目录 (modules/{moduleId})
        var installedDir = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (Directory.Exists(installedDir))
        {
            if (!await TryDeleteDirectoryAsync(installedDir, ct))
            {
                pendingDirs.Add(installedDir);
                _logger.LogWarning("[UninstallAsync] 已安装模块目录被锁定，已加入待删除队列: {Dir}", installedDir);
            }
            else
            {
                _logger.LogInformation("[UninstallAsync] 已删除已安装模块目录 {Dir}", installedDir);
            }
        }

        // 如果有目录无法删除，加入待删除队列
        if (pendingDirs.Count > 0)
        {
            if (_pendingDelete != null)
            {
                foreach (var dir in pendingDirs)
                {
                    _pendingDelete.AddPendingDelete(dir);
                }
            }
            return new ModuleOperationResult(
                true, 
                $"卸载成功，但有 {pendingDirs.Count} 个目录因 DLL 被锁定无法立即删除",
                pendingDirs
            );
        }

        return new ModuleOperationResult(true, "卸载成功");
    }

    /// <summary>
    /// 检查模块是否有待删除的目录（用于安装前检查）
    /// </summary>
    public bool HasPendingDelete(string moduleId)
    {
        if (_pendingDelete == null) return false;
        return _pendingDelete.HasPendingDeleteForModule(moduleId);
    }

    /// <summary>
    /// 获取模块的待删除目录列表
    /// </summary>
    public List<string> GetPendingDeleteDirs(string moduleId)
    {
        if (_pendingDelete == null) return new List<string>();
        return _pendingDelete.GetPendingDeletesForModule(moduleId);
    }

    /// <summary>
    /// 尝试删除目录，失败返回 false
    /// </summary>
    private async Task DeleteUniappPluginFilesAsync(string moduleId, List<string> pendingDirs, CancellationToken ct)
    {
        var pluginsRoot = FindUniappPluginsRoot();
        if (pluginsRoot == null)
        {
            _logger.LogInformation("[UninstallAsync] 未找到 UniApp 插件目录，跳过 UniApp 文件卸载");
            return;
        }

        var pluginDirs = ModulePluginDirectoryResolver.FindPluginDirectories(pluginsRoot, moduleId);
        foreach (var pluginDir in pluginDirs)
        {
            if (!await TryDeleteDirectoryAsync(pluginDir, ct))
            {
                pendingDirs.Add(pluginDir);
                _logger.LogWarning("[UninstallAsync] UniApp 插件目录被锁定，已加入待删除队列 {Dir}", pluginDir);
            }
            else
            {
                _logger.LogInformation("[UninstallAsync] 已删除 UniApp 插件目录 {Dir}", pluginDir);
            }
        }
    }

    private static string? FindUniappPluginsRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var pluginsRoot = Path.Combine(dir.FullName, "uniapp", "pgzx", "pages", "plugins");
                if (Directory.Exists(pluginsRoot))
                {
                    return pluginsRoot;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private async Task<bool> TryDeleteDirectoryAsync(string path, CancellationToken ct)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                // 先尝试清除只读属性
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (IOException) when (i < 2)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(300 * (i + 1), ct);
            }
            catch (UnauthorizedAccessException) when (i < 2)
            {
                await Task.Delay(200, ct);
            }
            catch
            {
                break;
            }
        }
        return false;
    }

    /// <summary>
    /// 查找模块源文件目录（开发环境）
    /// </summary>
    private string? FindModuleSourceDir(string moduleId)
    {
        // 从 DevModules:ServerSearch 配置查找
        var searchPaths = _config.GetSection("DevModules:ServerSearch").Get<string[]>();
        if (searchPaths != null)
        {
            foreach (var searchPath in searchPaths)
            {
                var fullPath = Path.IsPathRooted(searchPath)
                    ? searchPath
                    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), searchPath));

                var moduleDir = Path.Combine(fullPath, moduleId);
                if (Directory.Exists(moduleDir))
                    return moduleDir;
            }
        }

        // 默认路径
        var defaultPaths = new[]
        {
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Module", moduleId)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Module", moduleId)),
        };

        foreach (var path in defaultPaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// 查找模块的 install.json 路径（支持开发环境和生产环境）
    /// </summary>
    private string? FindInstallJsonPath(string moduleId)
    {
        _logger.LogInformation("[FindInstallJsonPath] 开始查找 moduleId={ModuleId}", moduleId);
        _logger.LogInformation("[FindInstallJsonPath] AppContext.BaseDirectory={BaseDir}", AppContext.BaseDirectory);
        _logger.LogInformation("[FindInstallJsonPath] Directory.GetCurrentDirectory()={CurDir}", Directory.GetCurrentDirectory());
        
        // 1. 生产环境：modules/{moduleId}/{version}/server/install.json（版本子目录布局，优先）
        var moduleVersionRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (Directory.Exists(moduleVersionRoot))
        {
            // 取版本号最大的子目录（降序排列取第一个）
            var versionDir = Directory.EnumerateDirectories(moduleVersionRoot)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (versionDir != null)
            {
                var versionedPath = Path.Combine(versionDir, "server", "install.json");
                _logger.LogInformation("[FindInstallJsonPath] 检查生产版本路径: {Path}, 存在={Exists}", versionedPath, File.Exists(versionedPath));
                if (File.Exists(versionedPath))
                    return versionedPath;
            }
        }
        // 1b. 旧布局兜底：modules/{moduleId}/install.json（向后兼容）
        var prodPath = Path.Combine(AppContext.BaseDirectory, "modules", moduleId, "install.json");
        _logger.LogInformation("[FindInstallJsonPath] 检查生产路径(旧布局): {Path}, 存在={Exists}", prodPath, File.Exists(prodPath));
        if (File.Exists(prodPath))
            return prodPath;

        // 2. 开发环境：从 DevModules:ServerSearch 配置查找
        var searchPaths = _config.GetSection("DevModules:ServerSearch").Get<string[]>();
        _logger.LogInformation("[FindInstallJsonPath] DevModules:ServerSearch={Paths}", string.Join(", ", searchPaths ?? Array.Empty<string>()));
        
        if (searchPaths != null)
        {
            foreach (var searchPath in searchPaths)
            {
                var fullPath = Path.IsPathRooted(searchPath)
                    ? searchPath
                    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), searchPath));
                _logger.LogInformation("[FindInstallJsonPath] 搜索路径: {SearchPath} -> {FullPath}", searchPath, fullPath);

                // 尝试 {searchPath}/{moduleId}/server/install.json
                var devPath = Path.Combine(fullPath, moduleId, "server", "install.json");
                _logger.LogInformation("[FindInstallJsonPath] 检查开发路径1: {Path}, 存在={Exists}", devPath, File.Exists(devPath));
                if (File.Exists(devPath))
                    return devPath;

                // 尝试 {searchPath}/{moduleId}/install.json
                devPath = Path.Combine(fullPath, moduleId, "install.json");
                _logger.LogInformation("[FindInstallJsonPath] 检查开发路径2: {Path}, 存在={Exists}", devPath, File.Exists(devPath));
                if (File.Exists(devPath))
                    return devPath;
            }
        }

        // 3. 默认开发路径
        var defaultDevPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Module", moduleId, "server", "install.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Module", moduleId, "server", "install.json"),
        };

        foreach (var path in defaultDevPaths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                _logger.LogInformation("[FindInstallJsonPath] 检查默认路径: {Path}, 存在={Exists}", fullPath, File.Exists(fullPath));
                if (File.Exists(fullPath))
                    return fullPath;
            }
            catch { }
        }

        _logger.LogWarning("[FindInstallJsonPath] 未找到 install.json for moduleId={ModuleId}", moduleId);
        return null;
    }

    /// <summary>
    /// DAG 拓扑检查（查找依赖此 moduleId 的所有已安装模块）
    /// </summary>
    private async Task<List<string>> CheckTopologyForUninstallAsync(string targetModuleId, CancellationToken ct)
    {
        var dependents = new List<string>();
        foreach (var m in _store.List())
        {
            if (m.Id.Equals(targetModuleId, StringComparison.OrdinalIgnoreCase)) continue;
            var installPath = FindInstallJsonPath(m.Id);
            if (installPath != null)
            {
                var moduleJsonPath = Path.Combine(Path.GetDirectoryName(installPath)!, "module.json");
                if (File.Exists(moduleJsonPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(moduleJsonPath, ct);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("dependencies", out var deps) && deps.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var d in deps.EnumerateArray())
                            {
                                if (d.GetString()?.Equals(targetModuleId, StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    dependents.Add(m.Name ?? m.Id);
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        return dependents;
    }

    /// <summary>
    /// 公开版本的 FindInstallJsonPath，供 Controller 调用。
    /// </summary>
    public string? FindInstallJsonPathPublic(string moduleId) => FindInstallJsonPath(moduleId);

    /// <summary>
    /// 安装前依赖检查：验证 manifest.Dependencies 中声明的所有依赖模块是否已安装且启用
    /// </summary>
    private ModuleOperationResult CheckDependencies(ModuleManifest manifest)
    {
        if (manifest.Dependencies == null || manifest.Dependencies.Length == 0)
            return new ModuleOperationResult(true, "无依赖");

        var installed = _store.List();
        var missing = new List<string>();

        foreach (var dep in manifest.Dependencies)
        {
            // 依赖格式支持 "moduleId" 或 "moduleId@version"
            var parts = dep.Split('@', 2);
            var depId = parts[0].Trim();
            var depVersion = parts.Length > 1 ? parts[1].Trim() : null;

            var match = installed.FirstOrDefault(m =>
                string.Equals(m.Id, depId, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                missing.Add($"{depId}（未安装）");
            }
            else if (!match.Enabled)
            {
                missing.Add($"{depId}（已安装但未启用）");
            }
            else if (depVersion != null && !IsVersionSatisfied(match.Version, depVersion))
            {
                missing.Add($"{depId}（需要 >= {depVersion}，当前 {match.Version}）");
            }
        }

        if (missing.Count > 0)
        {
            var msg = $"依赖检查失败：缺少以下依赖模块：{string.Join("、", missing)}";
            _logger.LogWarning("[CheckDependencies] 模块 {ModuleId} {Message}", manifest.Id, msg);
            return new ModuleOperationResult(false, msg);
        }

        return new ModuleOperationResult(true, "依赖检查通过");
    }

    /// <summary>
    /// 简单版本比较：当前版本是否满足所需最低版本
    /// </summary>
    private static bool IsVersionSatisfied(string? currentVersion, string requiredMinVersion)
    {
        if (string.IsNullOrEmpty(currentVersion)) return false;
        try
        {
            var current = Version.Parse(currentVersion.TrimStart('v', 'V'));
            var required = Version.Parse(requiredMinVersion.TrimStart('v', 'V'));
            return current >= required;
        }
        catch
        {
            // 版本格式无法解析时，按字符串比较
            return string.Compare(currentVersion, requiredMinVersion, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// 安装时将数据库存储模式的插件配置默认值写入 ginkgo_Sys_Settings（Module=moduleId）。
    /// </summary>
    private async Task SeedModuleConfigToDatabaseAsync(ModuleManifest manifest, string baseDir, CancellationToken ct)
    {
        if (manifest.Config?.IsDatabaseStorage != true) return;

        var configDir = Path.Combine(baseDir, "config");
        if (!Directory.Exists(configDir)) return;

        using var scope = _scopes.CreateScope();
        var dbSvc = scope.ServiceProvider.GetRequiredService<ModuleConfigDbService>();

        var primaryFile = manifest.Config.PrimaryFile;
        IEnumerable<string> sampleFiles;
        if (!string.IsNullOrWhiteSpace(primaryFile))
        {
            var sample = Path.Combine(configDir, primaryFile.EndsWith(".sample", StringComparison.OrdinalIgnoreCase) ? primaryFile : primaryFile + ".sample");
            sampleFiles = File.Exists(sample) ? new[] { sample } : Array.Empty<string>();
        }
        else
        {
            sampleFiles = Directory.GetFiles(configDir, "*.json.sample");
        }

        foreach (var samplePath in sampleFiles)
        {
            var fileName = Path.GetFileName(samplePath);
            if (fileName.EndsWith(".sample", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^".sample".Length];
            await dbSvc.SeedFromSampleAsync(manifest.Id, fileName, samplePath, ct);
            _logger.LogInformation("[Install] 已写入插件数据库配置默认值: {ModuleId}/{File}", manifest.Id, fileName);
        }
    }
}
