using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Ginkgo.Domain.Menus;
using System.Text.Json;
using System.Collections.Generic;



namespace Ginkgo.Application.Modules;

public interface IModuleAppService
{
    Task<bool> EnableAsync(string moduleId, CancellationToken ct = default);
    Task<bool> DisableAsync(string moduleId, CancellationToken ct = default);
    Task<ModuleInstallResult> InstallAsync(string moduleId, CancellationToken ct = default);
    Task<ModuleInstallResult> UpgradeAsync(string moduleId, CancellationToken ct = default);
    Task<ModuleInstallResult> UninstallAsync(string moduleId, CancellationToken ct = default);
    /// <summary>
    /// 获取模块运行时状态。
    /// </summary>
    /// <param name="moduleId">模块 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <param name="writeLog">
    /// 是否写入一条 <see cref="Ginkgo.Domain.Modules.ModuleStatusLogEntity"/> 日志。默认 true。
    /// 当 <c>GetInstalled</c> 等批量场景循环调用本方法时应传 false，避免一次列表刷新写入 N 条日志。
    /// </param>
    Task<ModuleStatusDto> GetStatusAsync(string moduleId, CancellationToken ct = default, bool writeLog = true);
}

public sealed class ModuleStatusDto
{
    public string ModuleId { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool Enabled { get; set; }
    public bool ServerDllLoaded { get; set; }
    public bool ServerConfigOk { get; set; }
    public bool LoadedInRuntime { get; set; }
    public bool ClientPresent { get; set; }
    /// <summary>
    /// 该模块的 install.json 中是否声明了菜单（即 Menus.RootCode 非空）。
    /// 用于前端区分「未安装菜单的插件不应再展示『菜单注册』状态」。
    /// </summary>
    public bool HasMenus { get; set; }
    public bool MenuRegistered { get; set; }
    public DateTime? ClientLastReportAtUtc { get; set; }
    public string? ClientStatus { get; set; }
    public string? Error { get; set; }
}

public sealed class ModuleAppService : IModuleAppService
{
    private readonly IRepository<InstalledModuleEntity> _repo;
    private readonly IRepository<ModuleStatusLogEntity> _logRepo;
    private readonly IRepository<ModuleClientReportEntity> _clientReportRepo;
    private readonly IRepository<Menu> _menuRepo;
    private readonly IModuleRuntimeQuery _runtimeQuery;
    private readonly IModuleInstallerPort _installer;

    public ModuleAppService(IRepository<InstalledModuleEntity> repo,
                             IRepository<ModuleStatusLogEntity> logRepo,
                             IRepository<ModuleClientReportEntity> clientReportRepo,
                             IRepository<Menu> menuRepo,
                             IModuleRuntimeQuery runtimeQuery,
                             IModuleInstallerPort installer)
    {
        _repo = repo; _logRepo = logRepo; _clientReportRepo = clientReportRepo; _menuRepo = menuRepo; _runtimeQuery = runtimeQuery; _installer = installer;
    }

    // 安全解析 install.json 的 Menus 片段（避免引用 API 层类型）
    private sealed class MenusSpecRoot { public MenusSpec? Menus { get; set; } }
    private sealed class MenusSpec { public string? RootCode { get; set; } public List<MenuItemSpec>? Items { get; set; } }
    private sealed class MenuItemSpec { public string? Route { get; set; } public bool Hidden { get; set; } }


    public async Task<bool> EnableAsync(string moduleId, CancellationToken ct = default)
    {
        var all = await _repo.GetAllAsync(ct);
        var e = all.FirstOrDefault(x => x.ModuleId == moduleId);
        if (e == null) return false;
        if (!e.Enabled)
        {
            e.Enabled = true;
            await _repo.UpdateAsync(e, ct);
        }
        return true;
    }

    public async Task<bool> DisableAsync(string moduleId, CancellationToken ct = default)
    {
        var all = await _repo.GetAllAsync(ct);
        var e = all.FirstOrDefault(x => x.ModuleId == moduleId);
        if (e == null) return false;
        if (e.Enabled)
        {
            e.Enabled = false;
            await _repo.UpdateAsync(e, ct);
        }
        return true;
    }

    public async Task<ModuleStatusDto> GetStatusAsync(string moduleId, CancellationToken ct = default, bool writeLog = true)
    {
        var all = await _repo.GetAllAsync(ct);
        var e = all.FirstOrDefault(x => x.ModuleId == moduleId);
        var dto = new ModuleStatusDto { ModuleId = moduleId };
        if (e == null)
        {
            dto.Error = "未安装该模块";
            return dto;
        }
        dto.Enabled = e.Enabled; dto.Version = e.Version;
        try
        {
            // 服务器端：运行时是否已加载（通过 IModuleRuntimeQuery 端口查询）
            dto.LoadedInRuntime = _runtimeQuery.IsLoaded(moduleId);

            // 服务器端：检查目录与配置（开发/生产自适配）
            bool isDevMode;
            var baseDir = ResolveModuleBaseDir(moduleId, e.Version, out isDevMode);
            var serverDir = Path.Combine(baseDir, "server");
            var cfgDir = Path.Combine(serverDir, "config");

            bool entryDllExists = false;
            if (Directory.Exists(serverDir))
            {
                // 递归查找以兼容 dev 下的 server/bin/... 与 prod 下的扁平布局
                entryDllExists = Directory.EnumerateFiles(serverDir, "*.dll", SearchOption.AllDirectories).Any();
            }
            bool hasJsonCfg = Directory.Exists(cfgDir) && Directory.EnumerateFiles(cfgDir, "*.json*", SearchOption.AllDirectories).Any();
            dto.ServerDllLoaded = entryDllExists;
            dto.ServerConfigOk = hasJsonCfg;

            // 客户端：基础判断（是否包含客户端、是否存在客户端目录）
            var clientDir = Path.Combine(baseDir, "client");
            dto.ClientPresent = e.HasClient && Directory.Exists(clientDir);

            // 客户端：读取最近一次客户端上报（如有）
            var allReports = await _clientReportRepo.GetAllAsync(ct);
            var last = allReports.Where(r => r.ModuleId == moduleId)
                                 .OrderByDescending(r => r.ReportedAtUtc)
                                 .FirstOrDefault();
            if (last != null)
            {
                dto.ClientLastReportAtUtc = last.ReportedAtUtc;
                dto.ClientStatus = last.Status;
            }

            // 菜单注册：根据 install.json 中的 Menus 规范联查 ginkgo_Sys_Menu
            // install.json 可能在 baseDir 或 baseDir/server 下
            var installJson = Path.Combine(baseDir, "install.json");
            if (!File.Exists(installJson))
            {
                installJson = Path.Combine(baseDir, "server", "install.json");
            }
            var menuOk = false;
            var hasMenus = false;
            if (File.Exists(installJson))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(installJson, ct);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var spec = JsonSerializer.Deserialize<MenusSpecRoot>(json, opts);
                    if (spec?.Menus?.RootCode != null && !string.IsNullOrWhiteSpace(spec.Menus.RootCode))
                    {
                        hasMenus = true;
                        var rootCode = spec.Menus.RootCode.Trim();
                        var menus = await _menuRepo.GetAllAsync(ct);
                        // 根菜单存在（未删除即认为已注册，Visible 仅控制显示）
                        bool rootExists = menus.Any(m => !m.IsDeleted
                            && (string.Equals(m.Code, rootCode, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(m.Route, rootCode, StringComparison.OrdinalIgnoreCase)));

                        bool itemsOk = true;
                        if (rootExists && spec.Menus.Items != null && spec.Menus.Items.Count > 0)
                        {
                            // 任一项目匹配即视为菜单已注册（允许管理员手动删除/调整个别子项）
                            itemsOk = spec.Menus.Items
                                .Where(it => !string.IsNullOrWhiteSpace(it.Route))
                                .Any(it => menus.Any(m => !m.IsDeleted
                                    && string.Equals((m.Route ?? string.Empty).Trim(), it.Route!.Trim(), StringComparison.OrdinalIgnoreCase)));
                        }
                        menuOk = rootExists && itemsOk;
                    }
                }
                catch { }
            }
            dto.HasMenus = hasMenus;
            dto.MenuRegistered = menuOk;
        }
        catch (Exception ex)
        {
            dto.Error = ex.Message;
        }
        // 记录一条状态日志（忽略异常）；批量调用场景下 writeLog=false 以免一次列表刷新写入 N 条。
        if (!writeLog) return dto;
        try
        {
            var log = new ModuleStatusLogEntity
            {
                Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
                ModuleId = moduleId,
                CheckedAtUtc = DateTime.Now,
                ServerDllLoaded = dto.ServerDllLoaded,
                ServerConfigOk = dto.ServerConfigOk,
                LoadedInRuntime = dto.LoadedInRuntime,
                ClientPresent = dto.ClientPresent,
                MenuRegistered = dto.MenuRegistered,
                Error = dto.Error,
                DetailsJson = null

            };
            await _logRepo.AddAsync(log, ct);
        }
        catch { }

        return dto;
    }
    public Task<ModuleInstallResult> InstallAsync(string moduleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("moduleId");
        return _installer.InstallAsync(moduleId, ct);
    }

    public Task<ModuleInstallResult> UpgradeAsync(string moduleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("moduleId");
        return _installer.UpgradeAsync(moduleId, ct);
    }

    public Task<ModuleInstallResult> UninstallAsync(string moduleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("moduleId");
        return _installer.UninstallAsync(moduleId, ct);
    }


    private string ResolveModuleBaseDir(string moduleId, string? version, out bool isDev)
    {
        isDev = false;
        try
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var devEnv = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
            if (devEnv)
            {
                foreach (var root in ProbeRoots())
                {
                    var devServer = Path.Combine(root, moduleId, "server");
                    if (Directory.Exists(devServer))
                    {
                        isDev = true;
                        return Path.Combine(root, moduleId);
                    }
                }
            }
        }
        catch { }
        // fallback to production layout
        return Path.Combine(AppContext.BaseDirectory, "modules", moduleId, version ?? "1.0.0");
    }

    private static IEnumerable<string> ProbeRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseDirs = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        
        foreach (var b in baseDirs)
        {
            var cur = new DirectoryInfo(b);
            // 向上递归查找 src/Module 目录（最多 8 层）
            for (int i = 0; i < 8 && cur != null; i++)
            {
                var probe = Path.Combine(cur.FullName, "src", "Module");
                if (Directory.Exists(probe) && seen.Add(probe))
                {
                    yield return probe;
                }
                cur = cur.Parent;
            }
        }
    }

}

