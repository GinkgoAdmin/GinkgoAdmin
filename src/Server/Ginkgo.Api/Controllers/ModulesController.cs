using Ginkgo.Api.Modules;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Ginkgo.Application.Modules;
using Ginkgo.Plugin.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Nodes;

using SqlSugar;

namespace Ginkgo.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/modules")]
// 安全基线：控制器级别强制走"权限"策略；超管在 PermissionAuthorizationHandler 内自动 bypass，
// 普通管理员需要在菜单表 Resource+Method 中具备授权才能访问；
// 仅明确标注 [AllowAnonymous] 的端点（如 enabled-plugins、WPF 客户端任务接口）才允许匿名访问。
[Authorize(Policy = "Permission")]
public sealed class ModulesController : ControllerBase
{
    private const string EnabledPluginsCacheKey = "modules:enabled-plugins";
    private readonly ModuleRepository _repo;
    private readonly InstalledModulesStore _store;
    private readonly ModuleInstaller _installer;
    private readonly ClientTaskService _clientTasks;
    private readonly IModuleAppService _moduleApp;
    private readonly ModuleHotReloader _hot;
    private readonly IHostEnvironment _env;
    private readonly IMemoryCache _memoryCache;


    public ModulesController(ModuleRepository repo, InstalledModulesStore store, ModuleInstaller installer, ClientTaskService clientTasks, IModuleAppService moduleApp, ModuleHotReloader hot, IHostEnvironment env, IMemoryCache memoryCache)
    {
        _repo = repo; _store = store; _installer = installer; _clientTasks = clientTasks; _moduleApp = moduleApp; _hot = hot; _env = env; _memoryCache = memoryCache;
    }

    /// <summary>
    /// 检查是否为开发环境，生产环境禁止模块操作
    /// </summary>
    private bool IsDevelopmentEnvironment() => string.Equals(_env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 返回生产环境禁止操作的错误响应
    /// </summary>
    private IActionResult ProductionModeError() => BadRequest(new { ok = false, message = "生产环境禁止模块安装、卸载和上传操作，请在开发环境中进行" });

    /// <summary>
    /// 获取已启用模块的ID列表（无需认证，供前端插件系统过滤使用）
    /// </summary>
    [AllowAnonymous]
    [HttpGet("enabled-plugins")]
    [EndpointComment("已启用插件清单", Category = "只读")]
    public async Task<ActionResult<IEnumerable<string>>> GetEnabledPlugins([FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        try
        {
            var enabledIds = await _memoryCache.GetOrCreateAsync(EnabledPluginsCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

                var entities = await moduleRepo.GetAllAsync();
                var ids = entities
                    .Where(x => x.Enabled)
                    .Select(x => x.ModuleId)
                    .ToList();

                var runtimeKnownIds = _store.List()
                    .Select(x => x.Id)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (runtimeKnownIds.Count > 0)
                {
                    ids = ids
                        .Where(runtimeKnownIds.Contains)
                        .ToList();
                }

                return ids;
            }) ?? new List<string>();

            return Ok(enabledIds);
        }
        catch
        {
            // 查询失败时返回空列表，前端将加载所有插件作为兜底
            return Ok(Array.Empty<string>());
        }
    }

    [HttpGet("repo")]
    [EndpointComment("扫描可安装模块仓库", Category = "只读")]
    public ActionResult<IEnumerable<ModuleManifest>> GetRepo() => Ok(_repo.ScanRepo().Select(x => x.Manifest));

#if DEBUG
    [AllowAnonymous]
    [HttpGet("debug/paths")]
    public IActionResult DebugPaths([FromQuery] string moduleId)
    {
        var cwd = Directory.GetCurrentDirectory();
        var baseDir = AppContext.BaseDirectory;
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        var probeResults = new List<object>();
        var baseDirs = new[] { cwd, baseDir };
        
        foreach (var b in baseDirs)
        {
            var cur = new DirectoryInfo(b);
            for (int i = 0; i < 8 && cur != null; i++)
            {
                var probe = Path.Combine(cur.FullName, "src", "Module");
                var exists = Directory.Exists(probe);
                probeResults.Add(new { path = probe, exists, level = i, from = b == cwd ? "cwd" : "baseDir" });
                
                if (exists && !string.IsNullOrEmpty(moduleId))
                {
                    var modulePath = Path.Combine(probe, moduleId);
                    var serverPath = Path.Combine(modulePath, "server");
                    var configPath = Path.Combine(serverPath, "config");
                    var dllPath = Directory.Exists(serverPath) 
                        ? Directory.EnumerateFiles(serverPath, "*.dll", SearchOption.AllDirectories).FirstOrDefault()
                        : null;
                    
                    probeResults.Add(new { 
                        modulePath, 
                        moduleExists = Directory.Exists(modulePath),
                        serverPath,
                        serverExists = Directory.Exists(serverPath),
                        configPath,
                        configExists = Directory.Exists(configPath),
                        dllPath,
                        dllExists = dllPath != null
                    });
                }
                
                cur = cur.Parent;
            }
        }
        
        return Ok(new { cwd, baseDir, env, probeResults });
    }
#endif

    [HttpGet("installed")]
    public async Task<ActionResult<IEnumerable<EnhancedModuleInfo>>> GetInstalled([FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        try
        {
            // 从数据库读取已安装模块的运行时信息
            var entities = await moduleRepo.GetAllAsync();
            var enhancedList = new List<EnhancedModuleInfo>();

            foreach (var entity in entities)
            {
                // 从 module.json 读取静态元数据
                var manifestPath = await GetModuleManifestPathAsync(entity.ModuleId);
                if (manifestPath == null)
                {
                    await moduleRepo.DeleteAsync(entity.Id);
                    _store.Remove(entity.ModuleId);
                    continue;
                }

                var manifest = await ReadModuleManifestAsync(entity.ModuleId);

                // 合并数据库信息和 module.json 信息
                var enhanced = new EnhancedModuleInfo
                {
                    // 数据库中的运行时信息
                    Id = entity.ModuleId,
                    Enabled = entity.Enabled,
                    InstalledAtUtc = entity.InstalledAtUtc,
                    CreatedAt = entity.CreatedAt,
                    UpdatedAt = entity.UpdatedAt,
                    CreatedBy = entity.CreatedBy?.ToString(),
                    UpdatedBy = entity.UpdatedBy?.ToString(),

                    // 优先使用 module.json 中的元数据，如果不存在则使用数据库中的值
                    Name = manifest?.Name ?? entity.Name,
                    Version = manifest?.Version ?? entity.Version,
                    HasClient = manifest?.HasClient ?? entity.HasClient,
                    Publisher = manifest?.Publisher ?? entity.Publisher,
                    Homepage = manifest?.Homepage ?? entity.Homepage,

                    // module.json 中的额外信息
                    Author = manifest?.Author,
                    Title = manifest?.Title,
                    MinAppVersion = manifest?.MinAppVersion,
                    Dependencies = manifest?.Dependencies,
                    HasPages = manifest?.HasPages ?? false,
                    TestRoute = manifest?.TestRoute,

                    // 环境和路径信息
                    IsDevMode = await IsModuleInDevModeAsync(entity.ModuleId),
                    ManifestPath = manifestPath
                };

                // 一次性填充运行时健康快照供前端列表渲染红/绿灯与菜单注册可见性。
                // writeLog=false 避免一次列表刷新写入 N 条 ModuleStatusLogEntity 日志；
                // 任何异常（e.g. install.json 损坏）都不阻断列表加载，只是该模块标记为 unknown。
                try
                {
                    var statusDto = await _moduleApp.GetStatusAsync(entity.ModuleId, default, writeLog: false);
                    enhanced.RuntimeLoaded = statusDto.LoadedInRuntime;
                    enhanced.ServerDllLoaded = statusDto.ServerDllLoaded;
                    enhanced.HasMenus = statusDto.HasMenus;
                    enhanced.MenuRegistered = statusDto.MenuRegistered;
                }
                catch
                {
                    // 单个模块状态评估失败时，保持默认 false，不影响列表整体加载
                }

                enhancedList.Add(enhanced);
            }

            // 更新内存存储（保持向后兼容）
            var dbIds = new HashSet<string>(enhancedList.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
            var stale = _store.List().Where(m => !dbIds.Contains(m.Id)).Select(m => m.Id).ToList();
            foreach (var id in stale) _store.Remove(id);

            foreach (var enhanced in enhancedList)
            {
                var legacy = new InstalledModule
                {
                    Id = enhanced.Id,
                    Name = enhanced.Name,
                    Version = enhanced.Version,
                    HasClient = enhanced.HasClient,
                    Enabled = enhanced.Enabled,
                    InstalledAtUtc = enhanced.InstalledAtUtc,
                    Publisher = enhanced.Publisher,
                    Homepage = enhanced.Homepage
                };
                await _store.AddOrUpdateAsync(legacy);
            }

            return Ok(enhancedList);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = $"获取模块列表失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 读取模块的 module.json 文件
    /// </summary>
    private async Task<ModuleManifest?> ReadModuleManifestAsync(string moduleId)
    {
        var manifestPath = await GetModuleManifestPathAsync(moduleId);
        if (manifestPath == null || !System.IO.File.Exists(manifestPath))
            return null;

        try
        {
            var json = await System.IO.File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<ModuleManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取模块 module.json 文件的路径
    /// </summary>
    private async Task<string?> GetModuleManifestPathAsync(string moduleId)
    {
        // 1. 开发环境：src/Module/{moduleId}/server/module.json
        if (await IsModuleInDevModeAsync(moduleId))
        {
            foreach (var root in ProbeRoots())
            {
                var devManifest = Path.Combine(root, moduleId, "server", "module.json");
                if (System.IO.File.Exists(devManifest))
                    return devManifest;
            }
        }

        // 2. 生产环境：modules/{moduleId}/{version}/module.json
        var baseRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (Directory.Exists(baseRoot))
        {
            var verDir = Directory.EnumerateDirectories(baseRoot)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (verDir != null)
            {
                var prodManifest = Path.Combine(verDir, "module.json");
                if (System.IO.File.Exists(prodManifest))
                    return prodManifest;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取开发环境的模块根目录列表
    /// </summary>
    private IEnumerable<string> ProbeRoots()
    {
        var roots = new List<string>();

        // 从配置或环境变量获取模块路径
        var config = HttpContext?.RequestServices?.GetService<IConfiguration>();
        var modulePath = config?.GetValue<string>("cudr.modulepath");
        if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
        {
            roots.Add(modulePath);
        }

        // 默认开发路径
        var defaultPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Module"),
            Path.Combine(AppContext.BaseDirectory, "src", "Module"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src", "Module"),
            // 添加更多可能的路径
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "src", "Module"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "Module")
        };

        foreach (var path in defaultPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var fullPath = Path.GetFullPath(path);
                    if (!roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                        roots.Add(fullPath);
                }
            }
            catch { }
        }

        return roots;
    }

    /// <summary>
    /// 判断模块是否运行在开发模式
    /// </summary>
    private Task<bool> IsModuleInDevModeAsync(string moduleId)
    {
        try
        {
            var env = HttpContext?.RequestServices?.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var isDev = string.Equals(env?.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
            if (!isDev) return Task.FromResult(false);

            // 检查开发环境路径是否存在
            foreach (var root in ProbeRoots())
            {
                var devPath = Path.Combine(root, moduleId, "server");
                if (Directory.Exists(devPath))
                    return Task.FromResult(true);
            }
        }
        catch { }
        return Task.FromResult(false);
    }

    private async Task<List<string>> ResolveAllModuleConfigDirsAsync(string moduleId, IRepository<InstalledModuleEntity> moduleRepo)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var env = HttpContext?.RequestServices?.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var isDev = string.Equals(env?.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
            if (isDev)
            {
                foreach (var root in ProbeRoots())
                {
                    var devCfg = Path.Combine(root, moduleId, "server", "config");
                    if (Directory.Exists(devCfg)) dirs.Add(devCfg);

                    var binDir = Path.Combine(root, moduleId, "server", "bin");
                    if (Directory.Exists(binDir))
                    {
                        var binConfigs = Directory.GetDirectories(binDir, "config", SearchOption.AllDirectories);
                        foreach (var bc in binConfigs) dirs.Add(bc);
                    }
                }
            }
        }
        catch { }

        var entities = await moduleRepo.GetAllAsync();
        var rec = entities.Where(x => x.ModuleId == moduleId)
                         .OrderByDescending(x => x.InstalledAtUtc)
                         .FirstOrDefault();
        if (rec != null)
        {
            var baseDir = Path.Combine(AppContext.BaseDirectory, "modules", moduleId, rec.Version);
            var cfgDir = Path.Combine(baseDir, "server", "config");
            if (Directory.Exists(cfgDir)) dirs.Add(cfgDir);
        }

        foreach (var root in ProbeRoots())
        {
            var devCfg = Path.Combine(root, moduleId, "server", "config");
            if (Directory.Exists(devCfg)) dirs.Add(devCfg);
        }

        return dirs.ToList();

        static IEnumerable<string> ProbeRoots()
        {
            var bases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (var b in bases)
            {
                var di = new DirectoryInfo(b);
                for (int i = 0; i < 6 && di != null; i++)
                {
                    yield return Path.Combine(di.FullName, "src", "Module");
                    di = di.Parent;
                }
            }
        }
    }

    private async Task<string?> ResolveModuleConfigDirAsync(string moduleId, IRepository<InstalledModuleEntity> moduleRepo)
    {
        // 1) 开发环境优先：src/Module/<moduleId>/server/config
        try
        {
            var env = HttpContext?.RequestServices?.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var isDev = string.Equals(env?.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
            if (isDev)
            {
                foreach (var root in ProbeRoots())
                {
                    var devCfg = Path.Combine(root, moduleId, "server", "config");
                    if (Directory.Exists(devCfg)) return devCfg;
                }
            }
        }
        catch { }

        // 2) 生产部署目录：<app>/modules/<moduleId>/<version>/server/config
        var entities = await moduleRepo.GetAllAsync();
        var rec = entities.Where(x => x.ModuleId == moduleId)
                         .OrderByDescending(x => x.InstalledAtUtc)
                         .FirstOrDefault();
        if (rec == null) return null;
        var baseDir = Path.Combine(AppContext.BaseDirectory, "modules", moduleId, rec.Version);
        var cfgDir = Path.Combine(baseDir, "server", "config");
        if (Directory.Exists(cfgDir)) return cfgDir;

        // 3) 兜底：尝试在运行目录附近向上搜索 src/Module
        foreach (var root in ProbeRoots())
        {
            var devCfg = Path.Combine(root, moduleId, "server", "config");
            if (Directory.Exists(devCfg)) return devCfg;
        }
        return null;

        static IEnumerable<string> ProbeRoots()
        {
            var bases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (var b in bases)
            {
                var di = new DirectoryInfo(b);
                for (int i = 0; i < 6 && di != null; i++)
                {
                    yield return Path.Combine(di.FullName, "src", "Module");
                    di = di.Parent;
                }
            }
        }
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig([FromQuery] string moduleId, [FromQuery] string file, [FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!IsConfigFileNameSafe(file)) return BadRequest(new { ok = false, message = "非法的文件名" });
        var cfgDir = await ResolveModuleConfigDirAsync(moduleId, moduleRepo);
        if (cfgDir == null) return NotFound(new { ok = false, message = "未找到模块或配置目录" });
        var full = Path.Combine(cfgDir, file);
        if (!System.IO.File.Exists(full)) return NotFound(new { ok = false, message = "配置文件不存在" });
        var text = System.IO.File.ReadAllText(full);
        return Content(text, "application/json");
    }

    [HttpGet("config/files")]
    public async Task<IActionResult> ListConfigFiles([FromQuery] string moduleId, [FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var cfgDir = await ResolveModuleConfigDirAsync(moduleId, moduleRepo);
        if (cfgDir == null) return NotFound(new { ok = false, message = "未找到模块或配置目录" });
        var files = Directory.Exists(cfgDir) ? Directory.GetFiles(cfgDir, "*.json*").Select(Path.GetFileName).ToArray() : Array.Empty<string>();
        return Ok(files);
    }

    public sealed record ConfigEdit(string Path, string RawJson);
    public sealed record ApplyConfigRequest(string ModuleId, string File, ConfigEdit[]? Edits, string? RawContent);

    [HttpPost("config/apply")]
    public async Task<IActionResult> ApplyConfig([FromBody] ApplyConfigRequest req, [FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!IsConfigFileNameSafe(req.File)) return BadRequest(new { ok = false, message = "非法的文件名" });
        var cfgDirs = await ResolveAllModuleConfigDirsAsync(req.ModuleId, moduleRepo);
        if (cfgDirs.Count == 0) return NotFound(new { ok = false, message = "未找到模块或配置目录" });

        var targetFiles = cfgDirs.Select(d => Path.Combine(d, req.File)).Where(System.IO.File.Exists).ToList();
        if (targetFiles.Count == 0) return NotFound(new { ok = false, message = "配置文件不存在" });

        var primaryFile = targetFiles.First();

        // 如果传入 RawContent，则优先整体替换
        if (!string.IsNullOrWhiteSpace(req.RawContent))
        {
            try
            {
                // 验证为合法 JSON 再写入
                var parsed = JsonNode.Parse(req.RawContent);
                var opts0 = new JsonSerializerOptions { WriteIndented = true };
                var jsonToWrite = parsed?.ToJsonString(opts0) ?? req.RawContent;
                foreach (var f in targetFiles)
                {
                    try { System.IO.File.WriteAllText(f, jsonToWrite); } catch { }
                }
                return Ok(new { ok = true, message = "保存成功" });
            }
            catch
            {
                return BadRequest(new { ok = false, message = "RawContent 不是合法 JSON" });
            }
        }

        JsonNode? root;
        try { root = JsonNode.Parse(System.IO.File.ReadAllText(primaryFile)); } catch { return BadRequest(new { ok = false, message = "配置文件 JSON 解析失败" }); }
        if (root is null) return BadRequest(new { ok = false, message = "配置文件为空" });

        foreach (var e in (req.Edits ?? Array.Empty<ConfigEdit>()))
        {
            try
            {
                var valNode = JsonNode.Parse(NormalizeRawJson(e.RawJson));
                ApplyPath(root, e.Path, valNode);
            }
            catch { return BadRequest(new { ok = false, message = $"应用修改失败: {e.Path}" }); }
        }

        var opts = new JsonSerializerOptions { WriteIndented = true };
        var finalJson = root.ToJsonString(opts);
        foreach (var f in targetFiles)
        {
            try { System.IO.File.WriteAllText(f, finalJson); } catch { }
        }
        return Ok(new { ok = true, message = "保存成功" });
    }

    // 访问路径：GET /api/v1/modules/config/normalized，由控制器类级 [Authorize(Policy="Permission")] 守护，
    // 超管在 PermissionAuthorizationHandler 自动放行，普通角色需要在菜单表中具备 Resource+Method 授权。
    [HttpGet("config/normalized")]
    public async Task<IActionResult> GetConfigNormalized([FromQuery] string moduleId, [FromQuery] string file, [FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!IsConfigFileNameSafe(file)) return BadRequest(new { ok = false, message = "非法的文件名" });
        var (cfgDir, cfgPath, samplePath) = await ResolveConfigPathsAsync(moduleId, file, moduleRepo);
        if (cfgDir == null) return NotFound(new { ok = false, message = "未找到模块或配置目录" });
        var usePath = cfgPath ?? samplePath;
        if (usePath == null) return NotFound(new { ok = false, message = "未找到配置文件或样例" });
        JsonNode? root;
        try { root = JsonNode.Parse(System.IO.File.ReadAllText(usePath)); } catch { return BadRequest(new { ok = false, message = "配置文件 JSON 解析失败" }); }
        if (root is null) return BadRequest(new { ok = false, message = "配置文件为空" });
        var normalized = NormalizeForUi(root);
        return Ok(normalized);
    }

    public sealed record SaveAndReloadRequest(string ModuleId, string File, JsonNode Content);

    [HttpPost("config/save-and-reload")]
    public async Task<IActionResult> SaveAndReloadAsync([FromBody] SaveAndReloadRequest req, [FromServices] IRepository<InstalledModuleEntity> moduleRepo, CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!IsConfigFileNameSafe(req.File)) return BadRequest(new { ok = false, message = "非法的文件名" });
        var cfgDirs = await ResolveAllModuleConfigDirsAsync(req.ModuleId, moduleRepo);
        if (cfgDirs.Count == 0) return NotFound(new { ok = false, message = "未找到模块或配置目录" });

        var targetFiles = cfgDirs.Select(d => Path.Combine(d, req.File)).ToList();
        var primaryFile = targetFiles.FirstOrDefault(System.IO.File.Exists) ?? targetFiles.First();

        // 读取旧值用于保留密码占位符（***）
        JsonNode? oldRoot = null;
        try { if (System.IO.File.Exists(primaryFile)) oldRoot = JsonNode.Parse(System.IO.File.ReadAllText(primaryFile)); } catch { }

        // 合并：若新内容 items 中存在 type=password 且 value==='***'，则保留旧值
        var newRoot = req.Content;
        TryPreservePasswordPlaceholders(oldRoot, newRoot);

        try
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            var finalJson = newRoot.ToJsonString(opts);
            foreach (var target in targetFiles)
            {
                try
                {
                    var dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllText(target, finalJson);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, message = $"保存失败: {ex.Message}" });
        }

        // 配置已写入文件，需要重启后端服务才能完全生效（不在此处做 Disable/Enable，避免热重载失败导致插件被禁用）
        return Ok(new
        {
            ok = true,
            message = "配置保存成功，重启后端服务后将完全生效"
        });
    }

    public sealed record ResetConfigRequest(string ModuleId, string File);

    [HttpPost("config/reset")]
    public async Task<IActionResult> ResetConfigAsync([FromBody] ResetConfigRequest req, [FromServices] IRepository<InstalledModuleEntity> moduleRepo, CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!IsConfigFileNameSafe(req.File)) return BadRequest(new { ok = false, message = "非法的文件名" });
        var (_, _, samplePath) = await ResolveConfigPathsAsync(req.ModuleId, req.File, moduleRepo);
        var cfgDirs = await ResolveAllModuleConfigDirsAsync(req.ModuleId, moduleRepo);
        if (cfgDirs.Count == 0) return NotFound(new { ok = false, message = "未找到模块或配置目录" });
        if (samplePath == null || !System.IO.File.Exists(samplePath)) return NotFound(new { ok = false, message = "未找到样例文件" });

        var targetFiles = cfgDirs.Select(d => Path.Combine(d, req.File)).ToList();

        try
        {
            foreach (var target in targetFiles)
            {
                try
                {
                    var dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    System.IO.File.Copy(samplePath, target, overwrite: true);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, message = $"重置失败: {ex.Message}" });
        }

        var disabled = await _hot.DisableAsync(req.ModuleId, ct);
        var enabled = await _hot.EnableAsync(req.ModuleId, ct);
        var ok = disabled && enabled;
        return Ok(new
        {
            ok,
            message = ok
                ? "已恢复默认并热重载，请重启后端服务确保配置完全生效"
                : "已恢复默认，但热重载失败，请重启后端服务使配置生效"
        });
    }

        [HttpDelete("config/delete")]
        public async Task<IActionResult> DeleteConfigAsync([FromQuery] string moduleId, [FromQuery] string file, [FromServices] IRepository<InstalledModuleEntity> moduleRepo, CancellationToken ct)
        {
            if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
                return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
            if (string.IsNullOrWhiteSpace(file))
                return BadRequest(new { ok = false, message = "file 必填" });
            if (!IsConfigFileNameSafe(file))
                return BadRequest(new { ok = false, message = "非法的文件名" });

            var cfgDirs = await ResolveAllModuleConfigDirsAsync(moduleId, moduleRepo);
            if (cfgDirs.Count == 0) return NotFound(new { ok = false, message = "未找到模块或配置目录" });
            
            var targetFiles = cfgDirs.Select(d => Path.Combine(d, file)).Where(System.IO.File.Exists).ToList();
            if (targetFiles.Count == 0) return NotFound(new { ok = false, message = "配置文件不存在" });

            try
            {
                foreach (var target in targetFiles)
                {
                    try { System.IO.File.Delete(target); } catch { }
                }
            }
            catch (Exception ex) { return BadRequest(new { ok = false, message = $"删除失败: {ex.Message}" }); }

            var disabled = await _hot.DisableAsync(moduleId, ct);
            var enabled = await _hot.EnableAsync(moduleId, ct);
            var ok = disabled && enabled;
            return Ok(new { ok = true, message = ok ? "删除成功并已热重载" : "删除成功，但热重载失败" });
        }


    /// <summary>
    /// 校验配置文件名是否安全：禁止路径分隔符、..穿越、绝对路径等。
    /// </summary>
    private static bool IsConfigFileNameSafe(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        // 禁止路径分隔符
        if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0) return false;
        // 禁止 .. 穿越
        if (fileName.Contains("..")) return false;
        // 禁止绝对路径（Windows 盘符或 UNC）
        if (Path.IsPathRooted(fileName)) return false;
        // 归一化后再次验证文件名部分 == 原始输入
        var normalized = Path.GetFileName(fileName);
        if (!string.Equals(normalized, fileName, StringComparison.Ordinal)) return false;
        return true;
    }

    private async Task<(string? CfgDir, string? CfgPath, string? SamplePath)> ResolveConfigPathsAsync(string moduleId, string file, IRepository<InstalledModuleEntity> moduleRepo)
    {
        if (!IsConfigFileNameSafe(file)) return (null, null, null);
        var dir = await ResolveModuleConfigDirAsync(moduleId, moduleRepo);
        if (dir == null) return (null, null, null);
        var path = Path.Combine(dir, file);
        var sample = path.EndsWith(".sample", StringComparison.OrdinalIgnoreCase) ? path : path + ".sample";
        if (!System.IO.File.Exists(path)) path = null;
        if (!System.IO.File.Exists(sample)) sample = null;
        return (dir, path, sample);
    }

    private static JsonObject NormalizeForUi(JsonNode root)
    {
        // 若已是扁平化格式，直接返回（但需对密码做脱敏）
        if (root is JsonObject obj && obj["items"] is JsonArray items)
        {
            MaskPassword(items);
            return obj;
        }
        // 旧格式：转成默认分组
        var groups = new JsonArray
        {
            new JsonObject { ["code"] = "default", ["title"] = "默认", ["desc"] = "自动转换的配置" }
        };
        var itemsArr = new JsonArray();

        void AddItem(string name, JsonNode? val)
        {
            string type = "text";
            string displayName = name; // 简单回显
            object valueOut;
            if (val is JsonValue jv && jv.TryGetValue<bool>(out var b))
            {
                type = "radio"; valueOut = b ? "1" : "0";
            }
            else if (val is JsonValue jvs && jvs.TryGetValue<string>(out var s))
            {
                if (IsSecretKey(name)) { type = "password"; valueOut = string.IsNullOrEmpty(s) ? "" : "***"; }
                else { valueOut = s; }
            }
            else { valueOut = val is null ? "" : val.ToJsonString(); }

            var item = new JsonObject
            {
                ["group"] = "default",
                ["name"] = name,
                ["title"] = displayName,
                ["type"] = type,
                ["content"] = type == "radio" ? new JsonObject { ["1"] = "是", ["0"] = "否" } : new JsonObject(),
                ["value"] = JsonValue.Create(valueOut),
            };
            itemsArr.Add(item);
        }

        if (root is JsonObject ro)
        {
            foreach (var kv in ro)
            {
                if (kv.Value is JsonObject vo && vo["value"] != null)
                    AddItem(kv.Key, vo["value"]);
                else if (kv.Value is JsonValue vv)
                    AddItem(kv.Key, vv);
            }
        }
        else if (root is JsonArray ra)
        {
            int i = 0; foreach (var n in ra) { AddItem($"item[{i++}]", n); }
        }

        return new JsonObject { ["groups"] = groups, ["items"] = itemsArr };
    }

    private static void MaskPassword(JsonArray items)
    {
        foreach (var it in items.OfType<JsonObject>())
        {
            var type = it["type"]?.GetValue<string>();
            if (string.Equals(type, "password", StringComparison.OrdinalIgnoreCase))
            {
                var v = it["value"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(v)) it["value"] = "***";
            }
        }
    }

    private static bool IsSecretKey(string name)
        => name.EndsWith(".sk", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith(".secret", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith("SecretKey", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith("Password", StringComparison.OrdinalIgnoreCase);

    private static void TryPreservePasswordPlaceholders(JsonNode? oldRoot, JsonNode newRoot)
    {
        if (newRoot is not JsonObject no || no["items"] is not JsonArray items) return;
        Dictionary<string, string?> oldMap = new(StringComparer.OrdinalIgnoreCase);
        if (oldRoot is JsonObject oo && oo["items"] is JsonArray oldItems)
        {
            foreach (var it in oldItems.OfType<JsonObject>())
            {
                var name = it["name"]?.GetValue<string>();
                var type = it["type"]?.GetValue<string>();
                var val = it["value"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(name)) oldMap[name!] = val;
            }
        }
        foreach (var it in items.OfType<JsonObject>())
        {
            var type = it["type"]?.GetValue<string>();
            if (!string.Equals(type, "password", StringComparison.OrdinalIgnoreCase)) continue;
            var name = it["name"]?.GetValue<string>();
            var val = it["value"]?.GetValue<string>();
            if (name is null) continue;
            if (val == "***" && oldMap.TryGetValue(name, out var oldVal)) it["value"] = oldVal is null ? null : JsonValue.Create(oldVal);
        }
    }


    private static string NormalizeRawJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "\"\"";
        var t = raw.Trim();
        if (t.StartsWith("{") || t.StartsWith("[") || t.StartsWith("\"") || t.Equals("true", StringComparison.OrdinalIgnoreCase) || t.Equals("false", StringComparison.OrdinalIgnoreCase) || t.Equals("null", StringComparison.OrdinalIgnoreCase) || char.IsDigit(t[0]) || t.StartsWith("-"))
            return t;
        // treat as string literal
        return JsonSerializer.Serialize(raw);
    }

    private static void ApplyPath(JsonNode root, string path, JsonNode? value)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        JsonNode current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            string name = part;
            int? index = null;
            var lb = part.IndexOf('[');
            if (lb >= 0 && part.EndsWith("]"))
            {
                name = part.Substring(0, lb);
                var idxStr = part.Substring(lb + 1, part.Length - lb - 2);
                if (int.TryParse(idxStr, out var idx)) index = idx; else throw new Exception("index");
            }

            if (current is not JsonObject obj) throw new Exception("not object");
            if (!obj.TryGetPropertyValue(name, out var next) || next is null)
            {
                next = index.HasValue ? new JsonArray() : new JsonObject();
                obj[name] = next;
            }

            if (index.HasValue)
            {
                var arr = next as JsonArray ?? new JsonArray();
                if (next is not JsonArray) obj[name] = arr;
                while (arr.Count <= index.Value) arr.Add(null);
                if (i == parts.Length - 1)
                {
                    arr[index.Value] = value;
                    return;
                }
                current = arr[index.Value] ?? (arr[index.Value] = new JsonObject());
            }
            else
            {
                if (i == parts.Length - 1)
                {
                    obj[name] = value;
                    return;
                }
                current = obj[name] ?? (obj[name] = new JsonObject());
            }
        }
    }

    [HttpGet("installed/refresh-db")]
    public async Task<ActionResult<IEnumerable<InstalledModule>>> RefreshInstalledFromDb([FromServices] IRepository<InstalledModuleEntity> moduleRepo)
    {
        try
        {
            var entities = await moduleRepo.GetAllAsync();
            var list = entities.Select(x => new InstalledModule
            {
                Id = x.ModuleId,
                Name = x.Name,
                Version = x.Version,
                HasClient = x.HasClient,
                Enabled = x.Enabled,
                InstalledAtUtc = x.InstalledAtUtc,
                Publisher = x.Publisher,
                Homepage = x.Homepage,
                MenuRootCode = x.MenuRootCode
            }).ToList();

            foreach (var m in list) await _store.AddOrUpdateAsync(m);
        }
        catch { }
        return Ok(_store.List());
    }

    public sealed record InstallRequest(string ModuleId, string? Version);

    [HttpPost("install")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> InstallAsync([FromBody] InstallRequest req, CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var result = await _moduleApp.InstallAsync(req.ModuleId, ct);
        var message = result.Ok
            ? $"{result.Message}；请重启后端服务使安装生效"
            : result.Message;
        return Ok(new { ok = result.Ok, message });
    }

    [HttpPost("upgrade")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> UpgradeAsync([FromBody] InstallRequest req, CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var result = await _moduleApp.UpgradeAsync(req.ModuleId, ct);
        return Ok(new { ok = result.Ok, message = result.Message });
    }

    [HttpPost("uninstall")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> UninstallAsync([FromBody] InstallRequest req, CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var result = await _moduleApp.UninstallAsync(req.ModuleId, ct);
        var message = result.Ok
            ? $"{result.Message}；请重启后端服务使卸载生效"
            : result.Message;
        return Ok(new { ok = result.Ok, message });
    }

    public sealed record ToggleRequest(string ModuleId);

    [HttpPost("enable")]
    [EnableRateLimiting("module-ops")]
    public async Task<IActionResult> EnableAsync([FromBody] ToggleRequest req, [FromServices] ModuleSqlExecutor sqlExecutor)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var ok = await _moduleApp.EnableAsync(req.ModuleId, HttpContext.RequestAborted);
        if (!ok) return NotFound(new { ok = false, message = "未安装该模块" });
        // 同步内存
        var mem = _store.List().FirstOrDefault(x => x.Id == req.ModuleId);
        if (mem != null) mem.Enabled = true;

        // 启用菜单可见性（根据 install.json 的 RootCode）
        try
        {
            var installPath = _installer.FindInstallJsonPathPublic(req.ModuleId);
            if (installPath != null && System.IO.File.Exists(installPath))
            {
                var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
                if (spec?.Menus != null && !string.IsNullOrWhiteSpace(spec.Menus.RootCode))
                {
                    await sqlExecutor.SetMenuTreeVisibleAsync(spec.Menus.RootCode, true, HttpContext.RequestAborted);
                }
            }
        }
        catch { /* 菜单可见性设置失败不影响启用操作 */ }

        return Ok(new { ok = true, message = "已启用，重启后生效" });
    }

    [HttpPost("disable")]
    [EnableRateLimiting("module-ops")]
    public async Task<IActionResult> DisableAsync([FromBody] ToggleRequest req, [FromServices] ModuleSqlExecutor sqlExecutor)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var ok = await _moduleApp.DisableAsync(req.ModuleId, HttpContext.RequestAborted);
        if (!ok) return NotFound(new { ok = false, message = "未安装该模块" });
        // 同步内存
        var mem = _store.List().FirstOrDefault(x => x.Id == req.ModuleId);
        if (mem != null) mem.Enabled = false;

        // 禁用菜单可见性（根据 install.json 的 RootCode）
        try
        {
            var installPath = _installer.FindInstallJsonPathPublic(req.ModuleId);
            if (installPath != null && System.IO.File.Exists(installPath))
            {
                var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
                if (spec?.Menus != null && !string.IsNullOrWhiteSpace(spec.Menus.RootCode))
                {
                    await sqlExecutor.SetMenuTreeVisibleAsync(spec.Menus.RootCode, false, HttpContext.RequestAborted);
                }
            }
        }
        catch { /* 菜单可见性设置失败不影响禁用操作 */ }

        return Ok(new { ok = true, message = "已禁用，重启后生效" });
    }

    /// <summary>
    /// 重置模块菜单：删除该模块的所有菜单，然后根据 install.json 重新创建
    /// POST /api/v1/modules/reset-menus
    /// </summary>
    [HttpPost("reset-menus")]
    public async Task<IActionResult> ResetMenusAsync(
        [FromBody] ToggleRequest req,
        [FromServices] ModuleInstaller installer,
        [FromServices] ModuleSqlExecutor sqlExecutor,
        CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 查找 install.json
        var installPath = installer.FindInstallJsonPathPublic(req.ModuleId);
        if (installPath == null || !System.IO.File.Exists(installPath))
            return NotFound(new { ok = false, message = $"未找到模块 {req.ModuleId} 的 install.json 配置文件" });

        var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
        if (spec == null)
            return BadRequest(new { ok = false, message = "该模块的 install.json 解析失败" });

        // 后台 RBAC 菜单（ginkgo_Sys_Menu）与客户端入口菜单（MenuGroup，移动/桌面/前端）至少声明其一即可重置。
        var hasBackendMenus = spec.Menus != null && !string.IsNullOrWhiteSpace(spec.Menus.RootCode);
        var hasClientMenus = spec.ClientMenus != null && spec.ClientMenus.Count > 0;
        if (!hasBackendMenus && !hasClientMenus)
            return BadRequest(new { ok = false, message = "该模块的 install.json 中未定义任何菜单（Menus / ClientMenus）配置" });

        try
        {
            var moduleId = spec.ModuleId ?? req.ModuleId;

            // 1. 后台 RBAC 菜单：删除后按 install.json 重新创建（ginkgo_Sys_Menu）
            if (hasBackendMenus)
            {
                await sqlExecutor.RemoveMenusAsync(spec, ct);
                // moduleName 用于菜单展示名兜底，moduleId 用于归属隔离
                await sqlExecutor.ApplyMenusAsync(spec, moduleId, moduleId, ct);
            }

            // 2. 客户端入口菜单（移动 / 桌面 / 前端，写 ginkgo_Sys_MenuGroupItem）：
            //    按 Module 清理后，按 install.json 的 ClientMenus 重新注入到各端默认菜单组。
            await sqlExecutor.RemoveClientMenusByModuleAsync(moduleId, ct);
            if (hasClientMenus)
            {
                await sqlExecutor.ApplyClientMenusAsync(spec, moduleId, ct);
            }

            return Ok(new { ok = true, message = $"模块「{moduleId}」的菜单已重置（含后台菜单与客户端入口菜单）" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, message = $"重置菜单失败: {ex.Message}" });
        }
    }
    [HttpPost("remove-menus")]
    public async Task<IActionResult> RemoveMenusAsync(
        [FromBody] ToggleRequest req,
        [FromServices] ModuleInstaller installer,
        [FromServices] ModuleSqlExecutor sqlExecutor,
        CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        var installPath = installer.FindInstallJsonPathPublic(req.ModuleId);
        if (installPath == null || !System.IO.File.Exists(installPath))
            return NotFound(new { ok = false, message = $"未找到模块 {req.ModuleId} 的 install.json 配置文件" });

        var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
        if (spec == null)
            return BadRequest(new { ok = false, message = "该模块的 install.json 解析失败" });

        var hasBackendMenus = spec.Menus != null && !string.IsNullOrWhiteSpace(spec.Menus.RootCode);
        var hasClientMenus = spec.ClientMenus != null && spec.ClientMenus.Count > 0;
        if (!hasBackendMenus && !hasClientMenus)
            return BadRequest(new { ok = false, message = "该模块的 install.json 中未定义任何菜单（Menus / ClientMenus）配置" });

        try
        {
            var moduleId = spec.ModuleId ?? req.ModuleId;

            // 1. 后台 RBAC 菜单（ginkgo_Sys_Menu）
            if (hasBackendMenus)
            {
                await sqlExecutor.RemoveMenusAsync(spec, ct);
            }

            // 2. 客户端入口菜单（移动 / 桌面 / 前端，ginkgo_Sys_MenuGroupItem）按 Module 一并清理，含级联项授权
            await sqlExecutor.RemoveClientMenusByModuleAsync(moduleId, ct);

            return Ok(new { ok = true, message = $"模块「{moduleId}」的所有菜单已移除（含后台菜单与客户端入口菜单）" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, message = $"移除菜单失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 执行模块安装 SQL 脚本
    /// POST /api/v1/modules/run-install-sql
    /// 读取模块 install.json 中 SqlScripts 列表，在当前数据库中执行建表等脚本
    /// </summary>
    [HttpPost("run-install-sql")]
    public async Task<IActionResult> RunInstallSqlAsync(
        [FromBody] ToggleRequest req,
        [FromServices] ModuleInstaller installer,
        [FromServices] ModuleSqlExecutor sqlExecutor,
        CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 查找 install.json
        var installPath = installer.FindInstallJsonPathPublic(req.ModuleId);
        if (installPath == null || !System.IO.File.Exists(installPath))
            return NotFound(new { ok = false, message = $"未找到模块 {req.ModuleId} 的 install.json 配置文件" });

        var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
        if (spec?.SqlScripts == null || spec.SqlScripts.Length == 0)
            return BadRequest(new { ok = false, message = "该模块的 install.json 中未定义 SqlScripts" });

        var baseDir = Path.GetDirectoryName(installPath)!;
        var scriptPaths = spec.SqlScripts.Select(p => Path.Combine(baseDir, p)).ToList();

        // 检查文件是否存在
        var missing = scriptPaths.Where(p => !System.IO.File.Exists(p)).ToList();
        if (missing.Count > 0)
            return BadRequest(new { ok = false, message = $"以下 SQL 文件不存在: {string.Join(", ", missing.Select(Path.GetFileName))}" });

        try
        {
            await sqlExecutor.ExecuteScriptsAsync(scriptPaths, ct);
            return Ok(new { ok = true, message = $"已成功执行 {scriptPaths.Count} 个 SQL 脚本", executedScripts = spec.SqlScripts });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, message = $"执行安装 SQL 失败: {ex.Message}" });
        }
    }

    [HttpPost("hot/enable")]
    [EnableRateLimiting("module-ops")]
    public async Task<IActionResult> HotEnableAsync([FromBody] ToggleRequest req, CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        var ok = await _hot.EnableAsync(req.ModuleId, ct);
        return Ok(new { ok, message = ok ? "已热启用" : "热启用失败" });
    }

    [HttpPost("hot/disable")]
    [EnableRateLimiting("module-ops")]
    public async Task<IActionResult> HotDisableAsync([FromBody] ToggleRequest req, CancellationToken ct)
    {

        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var ok = await _hot.DisableAsync(req.ModuleId, ct);
        return Ok(new { ok, message = ok ? "已热禁用" : "热禁用失败（已标记禁用）" });
    }

    [HttpPost("hot/reload")]
    [EnableRateLimiting("module-ops")]
    public async Task<IActionResult> HotReloadAsync([FromBody] ToggleRequest req, CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        // 强制重新加载：先禁用再启用
        var disabled = await _hot.DisableAsync(req.ModuleId, ct);
        var enabled = await _hot.EnableAsync(req.ModuleId, ct);


        var ok = disabled && enabled;
        return Ok(new { ok, message = ok ? "已强制重新加载" : "强制重新加载失败" });
    }


    /// <summary>
    /// WPF 客户端拉取下发任务（安装/卸载等）。当前阶段仍走匿名通道，但已加严 clientId 字符集校验，
    /// 避免攻击者构造路径穿越或注入字符。后续 P3 将引入设备令牌做正式鉴权。
    /// </summary>
    [AllowAnonymous]
    [HttpGet("client/tasks")]
    public ActionResult<IEnumerable<ClientModuleTask>> GetClientTasks([FromQuery] string clientId)
    {
        if (!ModuleIdentifierValidator.IsSafeClientId(clientId))
            return BadRequest(new { ok = false, code = "INVALID_CLIENT_ID", message = "clientId 不合法" });

        return Ok(_clientTasks.Pull(clientId));
    }


	    [HttpPost("hot/install")]
	    [EnableRateLimiting("install")]
	    public async Task<IActionResult> HotInstallAsync([FromBody] InstallRequest req, CancellationToken ct)
	    {
	        if (!IsDevelopmentEnvironment()) return ProductionModeError();
	        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
	        var install = await _moduleApp.InstallAsync(req.ModuleId, ct);
	        if (!install.Ok) return Ok(new { ok = false, message = install.Message });
	        var enabled = await _hot.EnableAsync(req.ModuleId, ct);
	        if (enabled) return Ok(new { ok = true, message = "安装并热启用成功" });
	        // 回滚安装
	        var rollback = await _moduleApp.UninstallAsync(req.ModuleId, ct);
	        var msg = rollback.Ok ? "安装成功但热启用失败，已回滚安装" : $"安装成功但热启用失败，且回滚失败：{rollback.Message}";
	        return Ok(new { ok = false, message = msg });
	    }

    [HttpPost("hot/uninstall")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> HotUninstallAsync([FromBody] ToggleRequest req, CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var result = await _hot.UninstallAsync(req.ModuleId, ct);
        var message = result.Ok
            ? $"{result.Message}；请重启后端服务使卸载生效"
            : result.Message;
        return Ok(new { 
            ok = result.Ok, 
            message,
            pendingDeleteDirs = result.PendingDeleteDirs,
            hasPendingDelete = result.PendingDeleteDirs?.Count > 0
        });
    }

    /// <summary>
    /// 检查模块是否有待删除的目录（安装前检查）
    /// </summary>
    [HttpGet("pending-delete/{moduleId}")]
    public IActionResult CheckPendingDelete(string moduleId)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var hasPending = _installer.HasPendingDelete(moduleId);
        var dirs = _installer.GetPendingDeleteDirs(moduleId);
        return Ok(new { 
            hasPendingDelete = hasPending, 
            pendingDirs = dirs,
            message = hasPending 
                ? "该模块有待删除的目录，请重启服务或手动删除后再安装" 
                : null
        });
    }

    /// <summary>
    /// WPF 客户端拉取插件包。鉴权方式：JWT（支持 query 参数 access_token，由 AuthenticationSetup 中
    /// OnMessageReceived 路径白名单放行）。匿名访问已禁止（P0-2 安全修复）。
    /// 响应头会附带 X-Ginkgo-Package-SHA256，供客户端做完整性校验（P0-6）。
    /// </summary>
    [HttpGet("package")]
    [EnableRateLimiting("download")]
    public IActionResult GetPackage(
        [FromQuery] string name,
        [FromQuery] string version,
        [FromServices] ModuleSignatureVerifier verifier,
        [FromQuery] string side = "client")
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(name))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "模块名不合法" });
        if (!string.IsNullOrEmpty(version) && !ModuleIdentifierValidator.IsSafeVersion(version))
            return BadRequest(new { ok = false, code = "INVALID_VERSION", message = "版本号不合法" });

        var item = _repo.ScanRepo().FirstOrDefault(x => string.Equals(x.Manifest.Id, name, StringComparison.OrdinalIgnoreCase));
        if (item is null) return NotFound();
        var file = item.PackagePath;
        var fileName = Path.GetFileName(file);

        // P1-6：计算并写入 SHA256 响应头。如果哈希计算失败（文件丢失/读取错误），
        // 当 RequireDownloadHashHeader=true（生产默认）时直接 500 拒绝下发，
        // 避免客户端在没有完整性证据的前提下加载未知二进制。
        try
        {
            var sha256 = ModulePackageHashCache.GetOrCompute(file);
            Response.Headers["X-Ginkgo-Package-SHA256"] = sha256;
            // RFC 9530 Digest 头，标准化兼容
            Response.Headers["Digest"] = $"sha-256={Convert.ToBase64String(Convert.FromHexString(sha256))}";
        }
        catch (Exception ex)
        {
            if (verifier.Options.RequireDownloadHashHeader == true)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    code = "HASH_COMPUTE_FAILED",
                    message = $"模块包哈希计算失败，已拒绝下发：{ex.Message}"
                });
            }
            // 开发态宽松：允许下载但不附带哈希，客户端会在没有头时拒绝加载（P0-6 行为不变）
        }

        return PhysicalFile(file, "application/zip", fileName, enableRangeProcessing: true);
    }

    public sealed record ClientReport(string ClientId, string ModuleId, string Version, string Status, string? Error);

    /// <summary>
    /// WPF 客户端上报模块加载/安装结果。当前阶段保留匿名访问，但已加严输入校验，
    /// 限制 clientId / moduleId 格式与字段长度，避免投毒。后续 P3 将引入设备令牌正式鉴权。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("client/report")]
    public async Task<IActionResult> ClientReportAsync([FromBody] ClientReport report, [FromServices] IRepository<Ginkgo.Domain.Modules.ModuleClientReportEntity> repo)
    {
        if (report is null)
            return BadRequest(new { ok = false, message = "请求体为空" });
        if (!ModuleIdentifierValidator.IsSafeClientId(report.ClientId))
            return BadRequest(new { ok = false, code = "INVALID_CLIENT_ID", message = "clientId 不合法" });
        if (!ModuleIdentifierValidator.IsSafeModuleId(report.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!string.IsNullOrEmpty(report.Version) && !ModuleIdentifierValidator.IsSafeVersion(report.Version))
            return BadRequest(new { ok = false, code = "INVALID_VERSION", message = "version 不合法" });

        // 字段长度限幅，防御被构造超长字符串投毒
        var status = string.IsNullOrWhiteSpace(report.Status) ? "Unknown" : report.Status;
        if (status.Length > 64) status = status.Substring(0, 64);
        var error = report.Error;
        if (error != null && error.Length > 1024) error = error.Substring(0, 1024);

        var e = new Ginkgo.Domain.Modules.ModuleClientReportEntity
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            ModuleId = report.ModuleId,
            ClientId = report.ClientId,
            Version = report.Version,
            Status = status,
            Error = error,
            ReportedAtUtc = DateTime.Now,
        };
        await repo.AddAsync(e);
        return Ok(new { ok = true });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] string moduleId, CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var dto = await _moduleApp.GetStatusAsync(moduleId, ct);
        var isDev = await IsModuleInDevModeAsync(moduleId);
        var manifest = await ReadModuleManifestAsync(moduleId);
        var clientExpected = manifest?.Client?.EntryAssembly != null || manifest?.HasClient == true;
        var clientEntry = manifest?.Client?.EntryAssembly;
        var hasErrors = !string.IsNullOrWhiteSpace(dto.Error);
        // 按前端约定返回 camelCase 字段名
        return Ok(new
        {
            moduleId = dto.ModuleId,
            version = dto.Version,
            enabled = dto.Enabled,
            runtimeLoaded = dto.LoadedInRuntime,
            serverDllLoaded = dto.ServerDllLoaded,
            serverConfigOk = dto.ServerConfigOk,
            clientPresent = dto.ClientPresent,
            clientStatus = dto.ClientStatus,
            clientLastReportAtUtc = dto.ClientLastReportAtUtc,
            menuRegistered = dto.MenuRegistered,
            hasMenus = dto.HasMenus,
            hasErrors,
            isDevMode = isDev,
            clientExpected,
            clientEntryAssembly = clientEntry
        });

    }

    [HttpGet("{moduleId}/status-logs")]
    public async Task<IActionResult> GetStatusLogs([FromRoute] string moduleId, [FromServices] IRepository<ModuleStatusLogEntity> logRepo, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = logRepo.Query().Where(x => x.ModuleId == moduleId);
        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CheckedAtUtc)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return Ok(new { items = list, total, page, pageSize, totalPages });
    }

    #region 模块上传、安装、打包 API

    /// <summary>
    /// 上传模块包并验证
    /// POST /api/v1/modules/upload
    /// 支持 .zip 文件上传，解压并验证 install.json 和 module.json
    /// </summary>
    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB 限制
    public async Task<IActionResult> UploadModuleAsync(
        IFormFile file,
        [FromServices] ModuleUploadService uploadService,
        CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (file == null || file.Length == 0)
            return BadRequest(new { ok = false, message = "请选择要上传的模块包文件" });

        using var stream = file.OpenReadStream();
        var validation = await uploadService.UploadAndValidateAsync(stream, file.FileName, ct);

        if (!validation.IsValid)
        {
            return BadRequest(new { ok = false, message = validation.ErrorMessage });
        }

        return Ok(new
        {
            ok = true,
            message = "模块包验证成功",
            moduleId = validation.Manifest?.Id,
            moduleName = validation.Manifest?.Name,
            version = validation.Manifest?.Version,
            hasClient = validation.Manifest?.HasClient ?? false,
            publisher = validation.Manifest?.Publisher,
            // 一次性 uploadId，下一步 confirm-install 必传（P0-4）
            uploadId = validation.UploadId,
            // ⚠️ ExtractedPath 字段保留 30 天兼容期，新前端请改用 uploadId；该字段将来会被移除。
            extractedPath = validation.ExtractedPath,
            hasSqlScripts = validation.InstallSpec?.SqlScripts?.Length > 0,
            hasMenus = validation.InstallSpec?.Menus != null,
            // 供应链安全信息
            security = new
            {
                hashValid = validation.HashValidation?.IsValid ?? true,
                signatureValid = validation.SignatureValidation?.IsValid ?? true,
                signaturePublisher = validation.SignatureValidation?.MatchedPublisher,
                capabilities = validation.Manifest?.Capabilities,
                warnings = validation.SecurityWarnings
            }
        });
    }

    /// <summary>
    /// 上传并安装模块（一步完成）
    /// POST /api/v1/modules/upload-install
    /// </summary>
    [HttpPost("upload-install")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadAndInstallModuleAsync(
        IFormFile file,
        [FromServices] ModuleUploadService uploadService,
        CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (file == null || file.Length == 0)
            return BadRequest(new { ok = false, message = "请选择要上传的模块包文件" });

        using var stream = file.OpenReadStream();
        var validation = await uploadService.UploadAndValidateAsync(stream, file.FileName, ct);

        if (!validation.IsValid)
        {
            return BadRequest(new { ok = false, message = validation.ErrorMessage });
        }

        var installResult = await uploadService.InstallModuleAsync(validation, ct);

        if (!installResult.Ok)
        {
            return BadRequest(new
            {
                ok = false,
                message = installResult.Message,
                executedSteps = installResult.ExecutedSteps,
                rollbackSteps = installResult.RollbackSteps
            });
        }

        return Ok(new
        {
            ok = true,
            message = $"{installResult.Message}；请重启后端服务使安装生效",
            moduleId = installResult.ModuleId,
            version = installResult.Version,
            executedSteps = installResult.ExecutedSteps
        });
    }

    /// <summary>
    /// 确认安装已上传的模块（P0-4 安全修复）。
    /// 输入：uploadId（推荐，由 /upload 接口返回）；旧前端可继续传 extractedPath，但会再做一次路径前缀校验。
    /// 服务端会重新跑一遍完整校验（路径遍历、哈希、签名、版本兼容、SQL 脚本存在性），
    /// 不再相信客户端"已校验"状态字段。
    /// POST /api/v1/modules/confirm-install
    /// </summary>
    public sealed record ConfirmInstallRequest(string? UploadId, string? ExtractedPath);

    [HttpPost("confirm-install")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> ConfirmInstallAsync(
        [FromBody] ConfirmInstallRequest req,
        [FromServices] ModuleUploadService uploadService,
        CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (req == null) return BadRequest(new { ok = false, message = "请求体为空" });

        // 1) 确定解压目录路径来源：
        //    - 优先使用 uploadId 一次性 token（推荐）
        //    - 兼容老前端只传 ExtractedPath 的场景，但严格要求该路径在临时上传目录之下
        string? extractedPath = null;
        if (!string.IsNullOrWhiteSpace(req.UploadId))
        {
            extractedPath = uploadService.ConsumeUploadToken(req.UploadId);
            if (extractedPath == null)
                return StatusCode(410, new { ok = false, code = "UPLOAD_TOKEN_EXPIRED", message = "上传 token 已过期或不存在，请重新上传" });
        }
        else if (!string.IsNullOrWhiteSpace(req.ExtractedPath))
        {
            // 兼容路径，但必须落在 GetTempUploadDir() 之下
            if (!uploadService.IsPathUnderTempUploadDir(req.ExtractedPath))
                return BadRequest(new { ok = false, code = "INVALID_EXTRACTED_PATH", message = "extractedPath 必须位于服务端临时上传目录之下" });
            extractedPath = Path.GetFullPath(req.ExtractedPath);
            // 同步消费对应 token，避免重复 confirm
            var tokenForPath = uploadService.FindUploadTokenByPath(extractedPath);
            if (tokenForPath != null) uploadService.ConsumeUploadToken(tokenForPath);
        }
        else
        {
            return BadRequest(new { ok = false, message = "请传入 uploadId（推荐）或 extractedPath" });
        }

        if (!Directory.Exists(extractedPath))
            return BadRequest(new { ok = false, message = "上传的模块包已过期或不存在，请重新上传" });

        // 2) 重新跑一遍完整校验（哈希/签名/路径遍历/版本兼容/SQL 存在性）
        var validation = await uploadService.RevalidateAsync(extractedPath, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { ok = false, code = "REVALIDATION_FAILED", message = validation.ErrorMessage });
        }

        // 3) 安装
        var installResult = await uploadService.InstallModuleAsync(validation, ct);

        if (!installResult.Ok)
        {
            return BadRequest(new
            {
                ok = false,
                message = installResult.Message,
                executedSteps = installResult.ExecutedSteps,
                rollbackSteps = installResult.RollbackSteps
            });
        }

        return Ok(new
        {
            ok = true,
            message = $"{installResult.Message}；请重启后端服务使安装生效",
            moduleId = installResult.ModuleId,
            version = installResult.Version,
            executedSteps = installResult.ExecutedSteps
        });
    }

    /// <summary>
    /// 打包模块
    /// POST /api/v1/modules/package
    /// </summary>
    /// <summary>
    /// 打包请求参数
    /// </summary>
    /// <param name="ModuleId">模块ID</param>
    /// <param name="PackageType">打包类型：source-源码包，compiled-编译包</param>
    /// <param name="IncludeData">（旧参数，保持向后兼容）是否包含数据库数据</param>
    /// <param name="ExportDbSchema">是否从真实数据库导出表结构替代安装SQL</param>
    /// <param name="ExportDbData">是否从真实数据库导出表数据</param>
    /// <param name="ExportClientMenus">是否导出多客户端菜单到 install.json</param>
    /// <param name="ExportDictionary">是否导出插件字典到 ini_data.sql</param>
    /// <param name="SanitizeConfig">是否对插件配置文件做脱敏处理（清空 items[].value 真实值），默认 true</param>
    public sealed record PackageRequest(string ModuleId, string PackageType = "source", bool IncludeData = false, bool ExportDbSchema = false, bool ExportDbData = false, bool ExportClientMenus = false, bool ExportDictionary = false, bool SanitizeConfig = true);

    [HttpPost("package")]
    public async Task<IActionResult> PackageModuleAsync(
        [FromBody] PackageRequest req,
        [FromServices] ModulePackageService packageService,
        CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 向后兼容：旧的 IncludeData=true 等价于同时导出结构和数据
        var exportSchema = req.ExportDbSchema || req.IncludeData;
        var exportData = req.ExportDbData || req.IncludeData;

        // 校验：真实数据内容依赖真实数据结构
        if (exportData && !exportSchema)
            return BadRequest(new { ok = false, message = "勾选“真实数据内容”时必须同时勾选“真实数据结构”" });

        var result = await packageService.PackageModuleAsync(req.ModuleId, req.PackageType ?? "source", exportSchema, exportData, req.SanitizeConfig, ct, progress: null, exportClientMenus: req.ExportClientMenus, exportDictionary: req.ExportDictionary);

        if (!result.Ok)
        {
            return BadRequest(new { ok = false, message = result.Message, steps = result.Steps });
        }

        return Ok(new
        {
            ok = true,
            message = result.Message,
            fileName = result.FileName,
            fileSize = result.FileSize,
            packageType = result.PackageType,
            includedFiles = result.IncludedFiles.Count,
            steps = result.Steps,
            downloadUrl = $"/api/v1/modules/package/download?moduleId={req.ModuleId}&packageType={req.PackageType ?? "source"}&exportDbSchema={exportSchema}&exportDbData={exportData}&sanitizeConfig={req.SanitizeConfig}",
            localPath = result.PackagePath
        });
    }

    /// <summary>
    /// 下载打包的模块
    /// GET /api/v1/modules/package/download
    /// </summary>
    [HttpGet("package/download")]
    public async Task<IActionResult> DownloadPackageAsync(
        [FromQuery] string moduleId,
        [FromQuery] string packageType = "source",
        [FromQuery] bool includeData = false,
        [FromQuery] bool exportDbSchema = false,
        [FromQuery] bool exportDbData = false,
        [FromQuery] bool exportClientMenus = false,
        [FromQuery] bool exportDictionary = false,
        [FromQuery] bool sanitizeConfig = true,
        [FromServices] ModulePackageService packageService = null!,
        CancellationToken ct = default)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 向后兼容：旧的 includeData=true 等价于同时导出结构和数据
        var schema = exportDbSchema || includeData;
        var data = exportDbData || includeData;
        var result = await packageService.PackageModuleAsync(moduleId, packageType, schema, data, sanitizeConfig, ct, progress: null, exportClientMenus: exportClientMenus, exportDictionary: exportDictionary);

        if (!result.Ok || result.PackagePath == null)
        {
            return NotFound(new { ok = false, message = result.Message });
        }

        var fileStream = new FileStream(result.PackagePath, FileMode.Open, FileAccess.Read);
        return File(fileStream, "application/zip", result.FileName);
    }

    /// <summary>
    /// 获取可打包的模块列表
    /// GET /api/v1/modules/packageable
    /// </summary>
    [HttpGet("packageable")]
    public IActionResult GetPackageableModules([FromServices] ModulePackageService packageService)
    {
        var modules = packageService.GetPackageableModules();
        return Ok(new { ok = true, modules });
    }

    /// <summary>
    /// 增强的模块安装API - 支持开发/生产环境区分
    /// POST /api/v1/modules/install-enhanced
    /// 开发环境：复制源文件到src/Module目录，Web前端安装到web/src/plugins/installed/
    /// 生产环境：仅执行SQL和菜单注册
    /// 支持安装失败回滚
    /// </summary>
    public sealed record EnhancedInstallRequest(string ModuleId, bool? ForceDevMode = null);

    [HttpPost("install-enhanced")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> InstallEnhancedAsync(
        [FromBody] EnhancedInstallRequest req,
        [FromServices] ModuleUploadService uploadService,
        CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 查找模块包
        var repoItem = _repo.ScanRepo().FirstOrDefault(x => 
            string.Equals(x.Manifest.Id, req.ModuleId, StringComparison.OrdinalIgnoreCase));
        
        if (repoItem == null)
            return NotFound(new { ok = false, message = $"模块包未找到: {req.ModuleId}" });

        // 解压模块包进行安装
        using var zipStream = new FileStream(repoItem.PackagePath, FileMode.Open, FileAccess.Read);
        var validation = await uploadService.UploadAndValidateAsync(zipStream, Path.GetFileName(repoItem.PackagePath), ct);

        if (!validation.IsValid)
        {
            return BadRequest(new { ok = false, message = validation.ErrorMessage });
        }

        var installResult = await uploadService.InstallModuleAsync(validation, ct);

        if (!installResult.Ok)
        {
            return BadRequest(new
            {
                ok = false,
                message = installResult.Message,
                executedSteps = installResult.ExecutedSteps,
                rollbackSteps = installResult.RollbackSteps
            });
        }

        return Ok(new
        {
            ok = true,
            message = $"{installResult.Message}；请重启后端服务使安装生效",
            moduleId = installResult.ModuleId,
            version = installResult.Version,
            executedSteps = installResult.ExecutedSteps
        });
    }

    /// <summary>
    /// 获取当前环境信息
    /// GET /api/v1/modules/environment
    /// </summary>
    [HttpGet("environment")]
    public IActionResult GetEnvironmentInfo([FromServices] IHostEnvironment env)
    {
        var isDev = string.Equals(env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
        return Ok(new
        {
            ok = true,
            environment = env.EnvironmentName,
            isDevelopment = isDev,
            canInstall = isDev,
            description = isDev 
                ? "开发环境：支持安装源码包和编译包，前端文件自动部署到工作区" 
                : "生产环境：不支持在线安装插件，请在开发环境中安装后重新部署"
        });
    }

    /// <summary>
    /// 开发模式专用：触发整个 API 进程的自重启，让 ALC 重新扫描 modules 目录、加载新插件 DLL。
    /// 流程与 /api/install/restart 一致：先调度一个分离子进程在 ~3s 后用相同命令行拉起当前可执行文件，
    /// 随后延迟 800ms 调用 IHostApplicationLifetime.StopApplication() 停掉本进程，浏览器侧轮询
    /// /api/v1/modules/environment 直到响应正常即可视为重启完成。
    /// 仅开发环境允许调用，生产环境强制返回 400，避免误触导致生产中断。
    /// POST /api/v1/modules/restart-process
    /// </summary>
    [HttpPost("restart-process")]
    [EnableRateLimiting("install")]
    public IActionResult RestartProcess([FromServices] Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();

        var scheduled = false;
        string? scheduleError = null;
        try
        {
            scheduled = Bootstrap.SelfRestartHelper.TryScheduleSelfRestart(out scheduleError);
        }
        catch (Exception ex)
        {
            scheduleError = ex.Message;
            Console.WriteLine($"[MODULE-RESTART] Schedule self-restart failed: {ex.Message}");
        }

        // 延迟 800ms 再停掉当前进程，确保 HTTP 响应能完整返回到浏览器；
        // 若 autoRelaunch=false（如外部托管在 IIS / systemd 等），由守护进程负责拉起。
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            try { lifetime.StopApplication(); }
            catch (Exception ex) { Console.WriteLine($"[MODULE-RESTART] StopApplication failed: {ex.Message}"); }
        });

        return Ok(new { restarting = true, autoRelaunch = scheduled, message = scheduleError });
    }

    /// <summary>
    /// 获取模块安全配置状态
    /// GET /api/v1/modules/security-status
    /// </summary>
    [HttpGet("security-status")]
    public IActionResult GetSecurityStatus([FromServices] ModuleSignatureVerifier verifier)
    {
        var options = verifier.Options;
        return Ok(new
        {
            ok = true,
            requireSignature = options.RequireSignature,
            requireFileHashes = options.RequireFileHashes,
            trustedPublicKeyCount = options.TrustedPublicKeys.Count,
            trustedPublicKeyNames = options.TrustedPublicKeys.Keys.ToArray(),
            trustedPublishers = options.TrustedPublishers,
            hasTrustedPublishers = options.TrustedPublishers.Length > 0
        });
    }

    /// <summary>
    /// SQL Dry-Run 预检
    /// POST /api/v1/modules/dry-run
    /// </summary>
    [HttpPost("dry-run")]
    public async Task<IActionResult> DryRunAsync(
        [FromBody] ToggleRequest req,
        [FromServices] ModuleSqlExecutor sqlExecutor,
        CancellationToken ct)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        var installPath = _installer.FindInstallJsonPathPublic(req.ModuleId);
        if (installPath == null || !System.IO.File.Exists(installPath))
            return NotFound(new { ok = false, message = $"未找到模块 {req.ModuleId} 的 install.json" });

        var spec = ModuleSqlExecutor.ReadInstallJson(installPath);
        if (spec?.SqlScripts == null || spec.SqlScripts.Length == 0)
            return Ok(new { ok = true, message = "该模块没有 SQL 脚本，无需预检", errors = Array.Empty<string>() });

        var baseDir = Path.GetDirectoryName(installPath)!;
        var scripts = spec.SqlScripts.Select(p => Path.Combine(baseDir, p));
        var (dryRunOk, errors) = await sqlExecutor.DryRunScriptsAsync(scripts, ct);

        return Ok(new { ok = dryRunOk, message = dryRunOk ? "SQL 预检通过" : "SQL 预检发现错误", errors });
    }

    /// <summary>
    /// 获取模块的能力声明列表
    /// GET /api/v1/modules/capabilities/{moduleId}
    /// </summary>
    [HttpGet("capabilities/{moduleId}")]
    public async Task<IActionResult> GetCapabilities(
        string moduleId,
        [FromServices] ModuleCapabilityAuditor auditor)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var manifest = await ReadModuleManifestAsync(moduleId);
        if (manifest == null)
            return NotFound(new { ok = false, message = $"未找到模块 {moduleId}" });

        var capabilities = auditor.ParseCapabilities(manifest);
        return Ok(new { ok = true, moduleId, capabilities });
    }

    /// <summary>
    /// 设置灰度发布策略
    /// POST /api/v1/modules/grayscale
    /// </summary>
    public sealed record GrayscaleRequest(string ModuleId, string Channel = "beta", List<string>? TargetTenantIds = null, DateTime? StartTime = null, DateTime? EndTime = null, bool AutoPromote = false);

    [HttpPost("grayscale")]
    public IActionResult SetGrayscale(
        [FromBody] GrayscaleRequest req,
        [FromServices] ModuleGrayscaleService grayscale)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 灰度功能依赖版本子目录布局（modules/{id}/{version}/），无版本目录则不支持
        var moduleVersionRoot = Path.Combine(AppContext.BaseDirectory, "modules", req.ModuleId);
        var hasVersionDir = Directory.Exists(moduleVersionRoot)
            && Directory.EnumerateDirectories(moduleVersionRoot).Any();
        if (!hasVersionDir)
            return BadRequest(new { ok = false, message = $"模块 {req.ModuleId} 尚未部署版本目录，不支持灰度发布。请先完成模块部署后再设置灰度策略。" });

        var policy = new GrayscalePolicy
        {
            Channel = req.Channel,
            TargetTenantIds = req.TargetTenantIds,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            AutoPromote = req.AutoPromote,
            CreatedAt = DateTime.Now
        };
        grayscale.SetPolicy(req.ModuleId, policy);
        return Ok(new { ok = true, message = $"模块 {req.ModuleId} 灰度策略已设置" });
    }

    /// <summary>
    /// 获取所有灰度策略
    /// GET /api/v1/modules/grayscale
    /// </summary>
    [HttpGet("grayscale")]
    public IActionResult GetGrayscale([FromServices] ModuleGrayscaleService grayscale)
    {
        return Ok(new { ok = true, policies = grayscale.GetAllPolicies() });
    }

    /// <summary>
    /// 移除灰度策略（全量发布）
    /// DELETE /api/v1/modules/grayscale/{moduleId}
    /// </summary>
    [HttpDelete("grayscale/{moduleId}")]
    public IActionResult RemoveGrayscale(string moduleId, [FromServices] ModuleGrayscaleService grayscale)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // 移除策略前检查版本目录是否存在（目录不存在说明模块未部署，策略本就无效）
        var moduleVersionRoot = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (!Directory.Exists(moduleVersionRoot) || !Directory.EnumerateDirectories(moduleVersionRoot).Any())
            return BadRequest(new { ok = false, message = $"模块 {moduleId} 尚未部署版本目录，无法执行全量发布操作。" });

        grayscale.RemovePolicy(moduleId);
        return Ok(new { ok = true, message = $"模块 {moduleId} 灰度策略已移除，全量发布" });
    }

    /// <summary>
    /// 获取模块快照列表
    /// GET /api/v1/modules/snapshots/{moduleId}
    /// </summary>
    [HttpGet("snapshots/{moduleId}")]
    public IActionResult GetSnapshots(string moduleId, [FromServices] ModuleSnapshotService snapshot)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        var snapshots = snapshot.GetSnapshots(moduleId);
        return Ok(new { ok = true, moduleId, snapshots });
    }

    /// <summary>
    /// 从快照回滚模块
    /// POST /api/v1/modules/rollback/{moduleId}
    /// </summary>
    public sealed record RollbackRequest(string SnapshotVersion);

    [HttpPost("rollback/{moduleId}")]
    public async Task<IActionResult> RollbackAsync(
        string moduleId,
        [FromBody] RollbackRequest req,
        [FromServices] ModuleSnapshotService snapshot,
        [FromServices] ModuleSecurityAuditService audit,
        CancellationToken ct)
    {
        if (!IsDevelopmentEnvironment()) return ProductionModeError();
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });
        if (!ModuleIdentifierValidator.IsSafeVersion(req.SnapshotVersion))
            return BadRequest(new { ok = false, code = "INVALID_VERSION", message = "snapshotVersion 不合法" });

        // 查找模块目录
        var targetDir = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);

        var (ok, message) = await snapshot.RestoreFromSnapshotAsync(moduleId, req.SnapshotVersion, targetDir, ct);
        audit.AuditOperation(moduleId, "Rollback", message: message, details: new { snapshotVersion = req.SnapshotVersion });

        return Ok(new { ok, message });
    }

    /// <summary>
    /// 获取模块操作审计日志（P2 增强：支持按 moduleId / action 前缀 / level / 时间区间筛选）。
    /// 受控制器级 [Authorize(Policy="Permission")] 保护，超管直接放行；普通管理员需要在菜单表中
    /// 拥有 GET /api/v1/modules/audit-log 的资源授权。
    /// </summary>
    [HttpGet("audit-log")]
    [EnableRateLimiting("module-ops")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? moduleId,
        [FromQuery] string? action,
        [FromQuery] string? level,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromServices] IRepository<ModuleOpLogEntity> logRepo = null!)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        if (!string.IsNullOrEmpty(moduleId) && !ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        // P2：action 前缀允许字母数字、下划线、点（如 "Security." / "Install"），
        // 长度上限 64，避免任意字符进入 LIKE/相等比较。
        if (!string.IsNullOrEmpty(action))
        {
            if (action.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(action, "^[A-Za-z0-9_.\\-]{1,64}$"))
                return BadRequest(new { ok = false, code = "INVALID_ACTION", message = "action 参数不合法" });
        }

        // 仅允许已知 level 枚举值
        if (!string.IsNullOrEmpty(level)
            && !string.Equals(level, "INFO", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { ok = false, code = "INVALID_LEVEL", message = "level 仅支持 INFO / ERROR" });
        }

        var q = logRepo.Query();
        if (!string.IsNullOrWhiteSpace(moduleId))
            q = q.Where(x => x.ModuleId == moduleId);
        if (!string.IsNullOrWhiteSpace(action))
        {
            // 支持前缀匹配（例如 "Security." 命中所有安全告警）
            q = q.Where(x => x.Action == action || x.Action.StartsWith(action));
        }
        if (!string.IsNullOrWhiteSpace(level))
            q = q.Where(x => x.Level == level.ToUpperInvariant());
        if (dateFrom.HasValue)
            q = q.Where(x => x.CreatedAtUtc >= dateFrom.Value);
        if (dateTo.HasValue)
            q = q.Where(x => x.CreatedAtUtc <= dateTo.Value);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CreatedAtUtc)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync();

        return Ok(new { ok = true, items = list, total, page, pageSize });
    }

    #region 前端 NPM 依赖管理

    /// <summary>
    /// 查询模块的前端 npm 依赖列表及其安装状态
    /// GET /api/v1/modules/npm-deps?moduleId=xxx
    /// </summary>
    [HttpGet("npm-deps")]
    public IActionResult GetNpmDeps([FromQuery] string moduleId)
    {
        if (!ModuleIdentifierValidator.IsSafeModuleId(moduleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        var webDir = ResolveWebDirectory();
        if (webDir == null)
            return BadRequest(new { ok = false, message = "未找到 web 前端目录" });

        var pluginDir = FindPluginDirectoryByModuleId(webDir, moduleId);
        if (pluginDir == null)
            return NotFound(new { ok = false, message = $"未找到模块 {moduleId} 对应的前端插件目录" });

        var deps = ReadNpmDependencies(pluginDir);
        if (deps == null || deps.Count == 0)
            return Ok(new { ok = true, deps = Array.Empty<object>(), message = "该模块没有声明 npm 依赖" });

        // 检查每个依赖的安装状态
        var nodeModulesDir = Path.Combine(webDir, "node_modules");
        var result = deps.Select(d =>
        {
            var pkgDir = Path.Combine(nodeModulesDir, d.Name);
            var installed = false;
            string? installedVersion = null;
            try
            {
                var pkgJson = Path.Combine(pkgDir, "package.json");
                if (System.IO.File.Exists(pkgJson))
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(System.IO.File.ReadAllText(pkgJson));
                    if (json.TryGetProperty("version", out var v))
                    {
                        installedVersion = v.GetString();
                        installed = true;
                    }
                }
            }
            catch { }
            return new
            {
                name = d.Name,
                requiredVersion = d.Version,
                description = d.Description,
                required = d.Required,
                installed,
                installedVersion
            };
        }).ToList();

        return Ok(new { ok = true, deps = result, pluginDir = Path.GetFileName(pluginDir) });
    }

    /// <summary>
    /// 安装模块声明的前端 npm 依赖
    /// POST /api/v1/modules/install-npm-deps
    /// </summary>
    public sealed record InstallNpmDepsRequest(string ModuleId);

    [HttpPost("install-npm-deps")]
    public async Task<IActionResult> InstallNpmDepsAsync(
        [FromBody] InstallNpmDepsRequest req,
        [FromServices] NpmCommandRunner npmRunner,
        CancellationToken ct)
    {
        // 安全：moduleId 必须先过白名单校验，避免被构造为路径穿越或注入
        if (!ModuleIdentifierValidator.IsSafeModuleId(req.ModuleId))
            return BadRequest(new { ok = false, code = "INVALID_MODULE_ID", message = "moduleId 不合法" });

        var webDir = ResolveWebDirectory();
        if (webDir == null)
            return BadRequest(new { ok = false, message = "未找到 web 前端目录" });

        var pluginDir = FindPluginDirectoryByModuleId(webDir, req.ModuleId);
        if (pluginDir == null)
            return NotFound(new { ok = false, message = $"未找到模块 {req.ModuleId} 对应的前端插件目录" });

        var deps = ReadNpmDependencies(pluginDir);
        if (deps == null || deps.Count == 0)
            return Ok(new { ok = true, message = "该模块没有声明 npm 依赖，无需安装", installed = Array.Empty<string>() });

        // 检测可用的包管理器（优先 pnpm > npm）
        var packageManager = DetectPackageManager(webDir);

        var installedList = new List<string>();
        var errors = new List<string>();

        foreach (var dep in deps)
        {
            // 安全：name/version 必须通过白名单校验，杜绝命令注入（P0-3）
            if (!ModuleIdentifierValidator.IsSafeNpmPackageName(dep.Name))
            {
                errors.Add($"{dep.Name}: npm 包名不合法（疑似命令注入或拼写错误）");
                continue;
            }
            if (!ModuleIdentifierValidator.IsSafeNpmVersionSpec(dep.Version))
            {
                errors.Add($"{dep.Name}@{dep.Version}: version 字符集不合法");
                continue;
            }

            // 检查是否已安装
            var pkgJson = Path.Combine(webDir, "node_modules", dep.Name, "package.json");
            if (System.IO.File.Exists(pkgJson))
            {
                installedList.Add($"{dep.Name} (已存在，跳过)");
                continue;
            }

            var packageSpec = string.IsNullOrWhiteSpace(dep.Version) ? dep.Name : $"{dep.Name}@{dep.Version}";
            try
            {
                var (exitCode, output) = await npmRunner.InstallAsync(
                    packageManager: packageManager,
                    packageName: dep.Name,
                    versionSpec: dep.Version,
                    workDir: webDir,
                    extraFlags: null,
                    ct: ct);
                if (exitCode == 0)
                    installedList.Add($"{packageSpec} ✓");
                else
                    errors.Add($"{packageSpec}: {output}");
            }
            catch (Exception ex)
            {
                errors.Add($"{packageSpec}: {ex.Message}");
            }
        }

        var ok = errors.Count == 0;
        var message = ok
            ? $"成功安装 {installedList.Count} 个依赖"
            : $"安装完成，{installedList.Count} 个成功，{errors.Count} 个失败";

        return Ok(new { ok, message, installed = installedList, errors });
    }

    /// <summary>
    /// 定位 web 前端目录
    /// </summary>
    private string? ResolveWebDirectory()
    {
        // 从已知的 ProbeRoots 路径推算项目根目录
        foreach (var root in ProbeRoots())
        {
            // root 格式: {projectRoot}/src/Module
            var projectRoot = Path.GetFullPath(Path.Combine(root, "..", ".."));
            var webDir = Path.Combine(projectRoot, "web");
            if (Directory.Exists(webDir) && Directory.Exists(Path.Combine(webDir, "src", "plugins", "installed")))
                return webDir;
        }

        // 兜底：从 CWD 和 BaseDirectory 向上搜索
        var bases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var b in bases)
        {
            var di = new DirectoryInfo(b);
            for (int i = 0; i < 8 && di != null; i++)
            {
                var webDir = Path.Combine(di.FullName, "web");
                if (Directory.Exists(webDir) && Directory.Exists(Path.Combine(webDir, "src", "plugins", "installed")))
                    return webDir;
                di = di.Parent;
            }
        }
        return null;
    }

    /// <summary>
    /// 通过 moduleId 查找对应的前端插件目录
    /// 扫描 web/src/plugins/installed/*/module.json 和 plugin.json
    /// </summary>
    private static string? FindPluginDirectoryByModuleId(string webDir, string moduleId)
    {
        var pluginsBase = Path.Combine(webDir, "src", "plugins", "installed");
        if (!Directory.Exists(pluginsBase)) return null;

        foreach (var dir in Directory.GetDirectories(pluginsBase))
        {
            // 检查 module.json
            var moduleJsonPath = Path.Combine(dir, "module.json");
            if (System.IO.File.Exists(moduleJsonPath))
            {
                try
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(System.IO.File.ReadAllText(moduleJsonPath));
                    if (json.TryGetProperty("moduleId", out var mid) &&
                        string.Equals(mid.GetString(), moduleId, StringComparison.OrdinalIgnoreCase))
                        return dir;
                }
                catch { }
            }

            // 兜底：目录名匹配（去掉 Ginkgo.Module. 前缀，忽略大小写）
            var dirName = Path.GetFileName(dir);
            var shortId = moduleId.Replace("Ginkgo.Module.", "", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(dirName, shortId, StringComparison.OrdinalIgnoreCase))
                return dir;
        }
        return null;
    }

    /// <summary>
    /// 从插件目录读取 npmDependencies
    /// </summary>
    private static List<NpmDependencyItem> ReadNpmDependencies(string pluginDir)
    {
        var result = new List<NpmDependencyItem>();
        // 优先读 plugin.json，再读 module.json
        foreach (var fileName in new[] { "plugin.json", "module.json" })
        {
            var filePath = Path.Combine(pluginDir, fileName);
            if (!System.IO.File.Exists(filePath)) continue;
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(System.IO.File.ReadAllText(filePath));
                if (json.TryGetProperty("npmDependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in deps.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        result.Add(new NpmDependencyItem
                        {
                            Name = name!,
                            Version = item.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "",
                            Description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                            Required = item.TryGetProperty("required", out var r) && r.GetBoolean()
                        });
                    }
                    if (result.Count > 0) return result;
                }
            }
            catch { }
        }
        return result;
    }

    private sealed class NpmDependencyItem
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Required { get; set; }
    }

    /// <summary>
    /// 检测项目使用的包管理器
    /// </summary>
    private static string DetectPackageManager(string webDir)
    {
        if (System.IO.File.Exists(Path.Combine(webDir, "pnpm-lock.yaml"))) return "pnpm";
        if (System.IO.File.Exists(Path.Combine(webDir, "yarn.lock"))) return "yarn";
        return "npm";
    }

    // 注：原 RunNpmInstallAsync 静态辅助方法已移除（P0-3）。
    // npm 命令执行统一改用 NpmCommandRunner（@/Modules/NpmCommandRunner.cs），
    // 通过 ProcessStartInfo.ArgumentList 单独添加每个参数，杜绝 cmd.exe /c 路径下的命令注入。

    #endregion

    /// <summary>
    /// 在目录中查找文件
    /// </summary>
    private static string? FindFileInDirectory(string directory, string fileName)
    {
        var rootFile = Path.Combine(directory, fileName);
        if (System.IO.File.Exists(rootFile))
            return rootFile;

        var serverFile = Path.Combine(directory, "server", fileName);
        if (System.IO.File.Exists(serverFile))
            return serverFile;

        try
        {
            var files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
            return files.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
