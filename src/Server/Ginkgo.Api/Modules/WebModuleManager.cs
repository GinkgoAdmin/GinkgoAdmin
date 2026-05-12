using System.Text.Json;
using System.Text.RegularExpressions;
using Ginkgo.Domain.Modules;

namespace Ginkgo.Api.Modules;

/// <summary>
/// Web 前端模块管理器
/// 负责在模块安装/卸载时将前端文件安装到 web/src/plugins/installed/{shortName}/ 目录，
/// 并自动生成 plugin.json 和 index.ts 以接入前端插件系统。
/// </summary>
public sealed class WebModuleManager
{
    private readonly IConfiguration _config;
    private readonly ILogger<WebModuleManager> _logger;
    private readonly NpmCommandRunner _npmRunner;

    public WebModuleManager(IConfiguration config, ILogger<WebModuleManager> logger, NpmCommandRunner npmRunner)
    {
        _config = config;
        _logger = logger;
        _npmRunner = npmRunner;
    }

    /// <summary>
    /// 安装模块的 Web 前端文件到 plugins/installed/{shortName}/
    /// </summary>
    public async Task InstallWebFilesAsync(string moduleId, string moduleBaseDir, CancellationToken ct = default)
    {
        try
        {
            // 前端源目录：优先使用打包约定的 web-plugin/（来自 web/src/plugins/installed/{shortName}/），
            // 兼容模块自带的 web/ 目录
            var webSourceDir = Path.Combine(moduleBaseDir, "web-plugin");
            if (!Directory.Exists(webSourceDir))
            {
                webSourceDir = Path.Combine(moduleBaseDir, "web");
            }
            if (!Directory.Exists(webSourceDir))
            {
                _logger.LogInformation("[WebModuleManager] 模块 {ModuleId} 没有 web-plugin/ 或 web/ 目录，跳过前端安装", moduleId);
                return;
            }

            var webTargetRoot = GetWebTargetRoot();
            if (string.IsNullOrEmpty(webTargetRoot))
            {
                _logger.LogWarning("[WebModuleManager] 未找到 web 前端目录，跳过前端安装");
                return;
            }

            // 目标路径：优先使用 install-manifest.json 中 installPaths["web-plugin"]，否则用 shortName
            var shortName = ExtractShortName(moduleId);
            string pluginDir;
            var manifestTarget = TryReadWebPluginTargetFromManifest(moduleBaseDir);
            if (!string.IsNullOrEmpty(manifestTarget))
            {
                // manifestTarget 形如 "web/src/plugins/installed/aicore/"，相对于仓库根
                var repoRoot = Path.GetFullPath(Path.Combine(webTargetRoot, ".."));
                pluginDir = Path.GetFullPath(Path.Combine(repoRoot, manifestTarget.Replace('/', Path.DirectorySeparatorChar)));
                // 尝试更新 shortName 以与目标目录名对齐（影响 plugin.json id 前缀一致性）
                var targetDirName = Path.GetFileName(pluginDir.TrimEnd(Path.DirectorySeparatorChar));
                if (!string.IsNullOrEmpty(targetDirName))
                {
                    shortName = targetDirName;
                }
            }
            else
            {
                pluginDir = Path.Combine(webTargetRoot, "src", "plugins", "installed", shortName);
            }

            // 复制完整 web 目录内容到插件目录
            Directory.CreateDirectory(pluginDir);
            await CopyDirectoryAsync(webSourceDir, pluginDir, ct);
            _logger.LogInformation("[WebModuleManager] 已复制模块 {ModuleId} 前端文件到 {PluginDir}", moduleId, pluginDir);

            // 修复 Vue 文件中的 API 导入路径（相对路径调整）
            var viewsDir = Path.Combine(pluginDir, "views");
            if (Directory.Exists(viewsDir))
            {
                await FixVueImportsAsync(pluginDir, viewsDir, ct);
            }

            // 读取 module.json 并生成 plugin.json + index.ts
            // module.json 可能不在 web-plugin/ 中，需要回退到模块根的 server/module.json
            var moduleJsonPath = Path.Combine(pluginDir, "module.json");
            if (!File.Exists(moduleJsonPath))
            {
                var fallback = Path.Combine(moduleBaseDir, "server", "module.json");
                if (!File.Exists(fallback))
                    fallback = Path.Combine(moduleBaseDir, "module.json");
                if (File.Exists(fallback))
                {
                    File.Copy(fallback, moduleJsonPath, overwrite: true);
                }
            }
            if (File.Exists(moduleJsonPath))
            {
                await GeneratePluginFilesAsync(moduleId, shortName, pluginDir, moduleJsonPath, ct);
            }
            else
            {
                _logger.LogWarning("[WebModuleManager] 模块 {ModuleId} 缺少 module.json，跳过插件文件生成", moduleId);
            }

            // 安装插件声明的 npm 依赖（仅源码开发模式下执行）
            await InstallNpmDependenciesAsync(pluginDir, webTargetRoot, ct);

            _logger.LogInformation("[WebModuleManager] 模块 {ModuleId} Web 前端插件安装完成", moduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebModuleManager] 安装模块 {ModuleId} Web 前端失败", moduleId);
        }
    }

    /// <summary>
    /// 从模块目录的 install-manifest.json 读取 installPaths["web-plugin"]
    /// </summary>
    private static string? TryReadWebPluginTargetFromManifest(string moduleBaseDir)
    {
        try
        {
            var manifestPath = Path.Combine(moduleBaseDir, "install-manifest.json");
            if (!File.Exists(manifestPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("installPaths", out var paths)
                && paths.TryGetProperty("web-plugin", out var wp)
                && wp.ValueKind == JsonValueKind.String)
            {
                return wp.GetString();
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 卸载模块的 Web 前端文件
    /// </summary>
    public async Task UninstallWebFilesAsync(string moduleId, CancellationToken ct = default)
    {
        try
        {
            var webTargetRoot = GetWebTargetRoot();
            if (string.IsNullOrEmpty(webTargetRoot))
            {
                _logger.LogWarning("[WebModuleManager] 未找到 web 前端目录，跳过前端卸载");
                return;
            }

            var pluginsRoot = Path.Combine(webTargetRoot, "src", "plugins", "installed");
            var pluginDirs = ModulePluginDirectoryResolver.FindPluginDirectories(pluginsRoot, moduleId);

            // 在删除插件目录之前，先处理 npm 依赖的引用计数卸载
            foreach (var pluginDir in pluginDirs)
            {
                var pluginShortName = Path.GetFileName(pluginDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                await UninstallNpmDependenciesAsync(pluginDir, pluginShortName, webTargetRoot, ct);

                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, recursive: true);
                    _logger.LogInformation("[WebModuleManager] 已删除插件目录 {Dir}", pluginDir);
                }
            }

            _logger.LogInformation("[WebModuleManager] 模块 {ModuleId} Web 前端卸载完成", moduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebModuleManager] 卸载模块 {ModuleId} Web 前端失败", moduleId);
        }
    }

    /// <summary>
    /// 根据 module.json 生成 plugin.json 和 index.ts
    /// </summary>
    private async Task GeneratePluginFilesAsync(string moduleId, string shortName, string pluginDir, string moduleJsonPath, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(moduleJsonPath, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? shortName : shortName;
        var version = root.TryGetProperty("version", out var verProp) ? verProp.GetString() ?? "1.0.0" : "1.0.0";
        var description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";

        // 从 module.json 读取 npmDependencies 并同步到 plugin.json
        var npmDepsJson = "[]";
        if (root.TryGetProperty("npmDependencies", out var npmDepsProp) && npmDepsProp.ValueKind == JsonValueKind.Array)
        {
            npmDepsJson = npmDepsProp.GetRawText();
        }

        // 生成 plugin.json（同步 npmDependencies）
        // 注意：路由和菜单由系统动态菜单表驱动（web/src/router/admin.ts），
        // 插件自身不再在 index.ts 中注册 route:register / menu:register 钩子。
        var pluginJson = $$"""
        {
          "name": "{{shortName}}",
          "version": "{{version}}",
          "description": "{{EscapeJsonString(description)}}",
          "author": "GinkgoAdmin",
          "enabled": true,
          "hooks": [],
          "cdnDependencies": [],
          "npmDependencies": {{npmDepsJson}},
          "assets": []
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.json"), pluginJson, ct);

        // 生成 index.ts —— 仅当模块包未自带 index.ts 时才自动生成。
        // 组件解析规则：前端 web/src/router/admin.ts 中 resolvePluginComponent()
        // 会按菜单 code 前缀（如 'aicore:sessions'）定位插件目录，并按 route 末段的 kebab 名
        // 匹配 views/**\/*.vue 文件（AISessions.vue → ai-sessions）。
        //
        // 注意：如果模块包的 web-plugin/ 中已自带 index.ts（例如包含 layout:global 钩子注册），
        // 则保留模块自带版本，不覆盖。否则自动生成通用版本。
        var indexTsPath = Path.Combine(pluginDir, "index.ts");
        if (!File.Exists(indexTsPath))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("import type { Plugin } from '../../core/types'");
            sb.AppendLine();
            sb.AppendLine("// 本插件的路由与菜单均由数据库菜单表驱动（见 web/src/router/admin.ts 中的 resolvePluginComponent）。");
            sb.AppendLine("// 该文件由后端 WebModuleManager 自动生成，勿手动注册路由。");
            sb.AppendLine();
            sb.AppendLine("const plugin: Plugin = {");
            sb.AppendLine("  config: {");
            sb.AppendLine($"    name: '{shortName}',");
            sb.AppendLine($"    version: '{version}',");
            sb.AppendLine($"    description: '{EscapeJsString(description)}',");
            sb.AppendLine("    author: 'GinkgoAdmin',");
            sb.AppendLine("    enabled: true,");
            sb.AppendLine("    hooks: [],");
            sb.AppendLine("    cdnDependencies: [],");
            sb.AppendLine("    npmDependencies: [],");
            sb.AppendLine("    assets: []");
            sb.AppendLine("  },");
            sb.AppendLine("  async install(_api) {");
            sb.AppendLine("    // 无需手动注册：路由/菜单由菜单表驱动");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("export default plugin");

            await File.WriteAllTextAsync(indexTsPath, sb.ToString(), ct);
        }
        else
        {
            _logger.LogInformation("[WebModuleManager] 模块 {ModuleId} 已自带 index.ts，跳过自动生成（保留钩子注册等自定义逻辑）", moduleId);
        }
        _logger.LogInformation("[WebModuleManager] 已为模块 {ModuleId} 生成 plugin.json 和 index.ts（菜单驱动模式）", moduleId);
    }

    /// <summary>
    /// 修复 Vue 文件中的导入路径
    /// 插件目录结构: plugins/installed/{shortName}/views/admin/xxx.vue 和 plugins/installed/{shortName}/api/xxx.ts
    /// 需要将 ../../api/xxx 调整为正确的相对路径
    /// </summary>
    private async Task FixVueImportsAsync(string pluginDir, string viewsDir, CancellationToken ct)
    {
        var vueFiles = Directory.GetFiles(viewsDir, "*.vue", SearchOption.AllDirectories);
        foreach (var file in vueFiles)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(file, ct);
            var modified = false;

            // 计算从当前 Vue 文件到插件根目录的相对路径
            var fileDir = Path.GetDirectoryName(file)!;
            var relToPlugin = Path.GetRelativePath(fileDir, pluginDir).Replace('\\', '/');
            // relToPlugin 例如: ../.. (从 views/admin/ 到插件根)

            // 修复模块内的 api 相对导入
            // from '../../api/documents' -> from '{relToPlugin}/api/documents'
            var pattern = @"(from\s+['""])(?:\.\.\/)+api\/([^'""]+)(['""])";
            if (Regex.IsMatch(content, pattern))
            {
                content = Regex.Replace(content, pattern, $"$1{relToPlugin}/api/$2$3");
                modified = true;
            }

            if (modified)
            {
                await File.WriteAllTextAsync(file, content, ct);
                _logger.LogDebug("[WebModuleManager] 已修复文件 {File} 的导入路径", file);
            }
        }
    }

    /// <summary>
    /// 获取 web 前端项目根目录
    /// </summary>
    private string? GetWebTargetRoot()
    {
        var configPath = _config.GetValue<string>("WebModule:WebProjectPath");
        if (!string.IsNullOrEmpty(configPath) && Directory.Exists(configPath))
            return configPath;

        var baseDirs = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var baseDir in baseDirs)
        {
            var cur = new DirectoryInfo(baseDir);
            for (int i = 0; i < 8 && cur != null; i++)
            {
                var webDir = Path.Combine(cur.FullName, "web");
                var packageJson = Path.Combine(webDir, "package.json");
                if (File.Exists(packageJson))
                    return webDir;
                cur = cur.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// 提取模块短名称
    /// </summary>
    private static string ExtractShortName(string moduleId)
    {
        return ModulePluginDirectoryResolver.ExtractShortName(moduleId);
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private static async Task CopyDirectoryAsync(string sourceDir, string targetDir, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            await CopyFileAsync(file, targetFile, ct);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, targetSubDir, ct);
        }
    }

    private static async Task CopyFileAsync(string source, string target, CancellationToken ct)
    {
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        using var targetStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await sourceStream.CopyToAsync(targetStream, ct);
    }

    private static string EscapeJsonString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    private static string EscapeJsString(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");

    // ========== npm 依赖生命周期管理 ==========

    /// <summary>
    /// 判断当前是否为源码开发模式（有 package.json 且有 node_modules）
    /// 编译发布模式下前端已打包为静态文件，不需要执行 npm 命令
    /// </summary>
    private static bool IsDevMode(string webRoot)
    {
        var packageJson = Path.Combine(webRoot, "package.json");
        var nodeModules = Path.Combine(webRoot, "node_modules");
        return File.Exists(packageJson) && Directory.Exists(nodeModules);
    }

    /// <summary>
    /// 从 module.json 中解析 npmDependencies 数组
    /// 返回 (包名, 版本号) 列表
    /// </summary>
    private static List<(string name, string version)> ParseNpmDependencies(string moduleJsonPath)
    {
        var result = new List<(string name, string version)>();
        if (!File.Exists(moduleJsonPath)) return result;

        try
        {
            var json = File.ReadAllText(moduleJsonPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("npmDependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    var name = dep.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var version = dep.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(name))
                        result.Add((name, version));
                }
            }
        }
        catch { /* 解析失败时忽略 */ }
        return result;
    }

    /// <summary>
    /// 扫描所有已安装插件的 module.json，收集它们声明的 npm 依赖
    /// </summary>
    private static HashSet<string> CollectAllPluginNpmDeps(string webRoot, string? excludePlugin = null)
    {
        var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pluginsDir = Path.Combine(webRoot, "src", "plugins", "installed");
        if (!Directory.Exists(pluginsDir)) return allDeps;

        foreach (var dir in Directory.GetDirectories(pluginsDir))
        {
            var pluginName = Path.GetFileName(dir);
            // 排除当前正在卸载的插件
            if (excludePlugin != null && pluginName.Equals(excludePlugin, StringComparison.OrdinalIgnoreCase))
                continue;

            var moduleJson = Path.Combine(dir, "module.json");
            var pluginJson = Path.Combine(dir, "plugin.json");
            // 优先读取 module.json，如果不存在则尝试 plugin.json（兼容纯前端插件）
            var jsonFile = File.Exists(moduleJson) ? moduleJson : (File.Exists(pluginJson) ? pluginJson : null);
            if (jsonFile != null)
            {
                foreach (var (name, _) in ParseNpmDependencies(jsonFile))
                {
                    allDeps.Add(name);
                }
            }
        }
        return allDeps;
    }

    /// <summary>
    /// 安装插件声明的 npm 依赖
    /// 仅在源码开发模式下执行，会跳过已被其他插件安装的包。
    /// 安全：所有 name/version 在执行前必须通过 ModuleIdentifierValidator 白名单校验，
    /// 不合法依赖会被跳过并记录警告（P0-3 命令注入修复）。
    /// </summary>
    private async Task InstallNpmDependenciesAsync(string pluginDir, string webRoot, CancellationToken ct)
    {
        if (!IsDevMode(webRoot))
        {
            _logger.LogInformation("[WebModuleManager] 非源码开发模式，跳过 npm 依赖安装");
            return;
        }

        var moduleJsonPath = Path.Combine(pluginDir, "module.json");
        var deps = ParseNpmDependencies(moduleJsonPath);
        if (deps.Count == 0) return;

        foreach (var (name, version) in deps)
        {
            if (!ModuleIdentifierValidator.IsSafeNpmPackageName(name))
            {
                _logger.LogWarning("[WebModuleManager] 跳过不合法的 npm 包名: {Package}（疑似命令注入或拼写错误）", name);
                continue;
            }
            if (!ModuleIdentifierValidator.IsSafeNpmVersionSpec(version))
            {
                _logger.LogWarning("[WebModuleManager] 跳过包 {Package}：version 字符集不合法 \"{Version}\"", name, version);
                continue;
            }

            // 检查 node_modules 下是否已存在
            var pkgDir = Path.Combine(webRoot, "node_modules", name);
            if (Directory.Exists(pkgDir))
            {
                _logger.LogInformation("[WebModuleManager] npm 包 {Package} 已存在，跳过安装", name);
                continue;
            }

            _logger.LogInformation("[WebModuleManager] 正在安装 npm 包: {Package}@{Version}", name, version);
            try
            {
                var (code, output) = await _npmRunner.InstallAsync(
                    packageManager: "npm",
                    packageName: name,
                    versionSpec: version,
                    workDir: webRoot,
                    extraFlags: new[] { "--save" },
                    ct: ct);
                if (code == 0)
                {
                    _logger.LogInformation("[WebModuleManager] npm install {Package} 执行成功", name);
                }
                else
                {
                    _logger.LogWarning("[WebModuleManager] npm install {Package} 退出码: {Code}, 输出: {Output}", name, code, output);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WebModuleManager] 执行 npm install {Package} 失败", name);
            }
        }
    }

    /// <summary>
    /// 卸载插件的 npm 依赖
    /// 通过引用计数机制判断：仅当没有其他插件也声明了同一依赖时才执行卸载。
    /// 安全：name 在执行前必须通过 ModuleIdentifierValidator.IsSafeNpmPackageName 白名单校验。
    /// </summary>
    private async Task UninstallNpmDependenciesAsync(string pluginDir, string pluginShortName, string webRoot, CancellationToken ct)
    {
        if (!IsDevMode(webRoot))
        {
            _logger.LogInformation("[WebModuleManager] 非源码开发模式，跳过 npm 依赖卸载");
            return;
        }

        var moduleJsonPath = Path.Combine(pluginDir, "module.json");
        var deps = ParseNpmDependencies(moduleJsonPath);
        if (deps.Count == 0) return;

        // 收集其他插件（排除当前插件）的所有依赖
        var otherPluginDeps = CollectAllPluginNpmDeps(webRoot, excludePlugin: pluginShortName);

        foreach (var (name, _) in deps)
        {
            if (!ModuleIdentifierValidator.IsSafeNpmPackageName(name))
            {
                _logger.LogWarning("[WebModuleManager] 跳过卸载不合法 npm 包名: {Package}", name);
                continue;
            }
            if (otherPluginDeps.Contains(name))
            {
                _logger.LogInformation("[WebModuleManager] npm 包 {Package} 仍被其他插件使用，跳过卸载", name);
                continue;
            }

            _logger.LogInformation("[WebModuleManager] 正在卸载 npm 包: {Package}", name);
            try
            {
                var (code, output) = await _npmRunner.UninstallAsync(
                    packageManager: "npm",
                    packageName: name,
                    workDir: webRoot,
                    extraFlags: new[] { "--save" },
                    ct: ct);
                if (code == 0)
                {
                    _logger.LogInformation("[WebModuleManager] npm uninstall {Package} 执行成功", name);
                }
                else
                {
                    _logger.LogWarning("[WebModuleManager] npm uninstall {Package} 退出码: {Code}, 输出: {Output}", name, code, output);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WebModuleManager] 执行 npm uninstall {Package} 失败", name);
            }
        }
    }
}
