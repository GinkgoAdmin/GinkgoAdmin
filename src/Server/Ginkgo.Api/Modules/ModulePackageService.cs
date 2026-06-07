using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块打包结果
/// </summary>
public sealed class ModulePackageResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? PackagePath { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string PackageType { get; set; } = "source";
    public List<string> IncludedFiles { get; set; } = new();
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// 模块打包服务 — 支持源码包/编译包两种模式，包含Web前端插件、UniApp插件、数据库导出和菜单同步
/// </summary>
public sealed class ModulePackageService
{
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<ModulePackageService> _logger;
    private readonly ModuleSqlExecutor _sqlExecutor;
    private readonly ModuleDotnetBuildService _dotnetBuild;

    public ModulePackageService(IHostEnvironment env, IConfiguration config, ILogger<ModulePackageService> logger, ModuleSqlExecutor sqlExecutor, ModuleDotnetBuildService dotnetBuild)
    {
        _env = env;
        _config = config;
        _logger = logger;
        _sqlExecutor = sqlExecutor;
        _dotnetBuild = dotnetBuild;
    }

    private static JsonSerializerOptions CreateReadableJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    /// <summary>
    /// 判断是否为开发环境
    /// </summary>
    private bool IsDevelopmentEnvironment()
    {
        return string.Equals(_env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取开发环境模块源码目录
    /// </summary>
    private string? GetDevModuleSourceDir()
    {
        if (!IsDevelopmentEnvironment())
            return null;

        // 优先使用 DevModules:ServerSearch 配置（与 DevModuleBootstrap 保持一致）
        var searchPaths = _config.GetSection("DevModules:ServerSearch").Get<string[]>();
        if (searchPaths != null && searchPaths.Length > 0)
        {
            foreach (var searchPath in searchPaths)
            {
                var fullPath = Path.IsPathRooted(searchPath)
                    ? searchPath
                    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), searchPath));
                
                if (Directory.Exists(fullPath))
                    return fullPath;
            }
        }

        // 兼容旧配置 cudr.modulepath
        var modulePath = _config.GetValue<string>("cudr.modulepath");
        if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
            return modulePath;

        // 默认路径
        var defaultPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Module"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "Module"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src", "Module")
        };

        foreach (var path in defaultPaths)
        {
            try
            {
                if (Directory.Exists(path))
                    return Path.GetFullPath(path);
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// 获取仓库根目录
    /// </summary>
    private string? GetRepoRoot()
    {
        var devDir = GetDevModuleSourceDir();
        if (devDir == null) return null;
        // src/Module -> 上两级就是仓库根目录
        var dir = new DirectoryInfo(devDir);
        return dir.Parent?.Parent?.FullName;
    }

    /// <summary>
    /// 获取打包输出目录（仓库根目录下的 dist/modules）
    /// </summary>
    private string GetPackageOutputDir()
    {
        var repoRoot = GetRepoRoot();
        var outputDir = repoRoot != null
            ? Path.Combine(repoRoot, "dist", "modules")
            : Path.Combine(AppContext.BaseDirectory, "dist", "modules");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        return outputDir;
    }

    /// <summary>
    /// 获取临时暂存目录（用于打包过程中的中间文件）
    /// </summary>
    private string GetTempStagingDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ginkgo_module_packages");
        if (!Directory.Exists(tempDir))
            Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>
    /// 打包模块（支持源码包和编译包）
    /// </summary>
    /// <param name="moduleId">模块ID</param>
    /// <param name="packageType">打包类型：source-源码包，compiled-编译包</param>
    /// <param name="exportDbSchema">是否从真实数据库导出表结构替代安装SQL</param>
    /// <param name="exportDbData">是否从真实数据库导出表数据（需同时开启 exportDbSchema）</param>
    /// <param name="sanitizeConfig">是否对插件配置文件做脱敏处理（清空 items[].value 真实值，仅保留键和结构），默认 true</param>
    /// <param name="ct">取消令牌</param>
    /// <param name="progress">可选进度回调（供打包插件实时写入步骤日志）</param>
    public async Task<ModulePackageResult> PackageModuleAsync(string moduleId, string packageType = "source", bool exportDbSchema = false, bool exportDbData = false, bool sanitizeConfig = true, CancellationToken ct = default, IProgress<string>? progress = null)
    {
        var result = new ModulePackageResult { PackageType = packageType };

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            result.Message = "模块ID不能为空";
            return result;
        }

        // 查找模块目录
        var moduleDir = FindModuleDirectory(moduleId);
        if (moduleDir == null)
        {
            result.Message = $"未找到模块: {moduleId}";
            return result;
        }

        try
        {
            // 读取 module.json 获取版本信息
            var moduleJsonPath = FindFile(moduleDir, "module.json");
            if (moduleJsonPath == null)
            {
                result.Message = "模块目录中未找到 module.json";
                return result;
            }

            var moduleJson = await File.ReadAllTextAsync(moduleJsonPath, ct);
            var manifest = JsonSerializer.Deserialize<ModuleManifest>(moduleJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest == null)
            {
                result.Message = "无法解析 module.json";
                return result;
            }

            // 创建临时暂存目录
            var tempDir = GetTempStagingDir();
            var packageId = Guid.NewGuid().ToString("N");
            var stagingDir = Path.Combine(tempDir, packageId);
            Directory.CreateDirectory(stagingDir);

            var version = manifest.Version ?? "1.0.0";
            var repoRoot = GetRepoRoot();

            var isCompiled = string.Equals(packageType, "compiled", StringComparison.OrdinalIgnoreCase);

            // ============================================================
            // 步骤 1: 收集后端文件
            //   - 源码包：拷贝 server/ 下全部源码（排除 bin/obj）
            //   - 编译包：dotnet publish server csproj → 仅拷贝 DLL/PDB/XML + 非源码资源（module.json/install.json/sql/config/data）
            // ============================================================
            result.Steps.Add("[1/10] 收集后端文件...");
            var serverDir = Path.Combine(moduleDir, "server");
            if (Directory.Exists(serverDir))
            {
                if (isCompiled)
                {
                    await CollectServerCompiledAsync(serverDir, stagingDir, result, ct);
                }
                else
                {
                    CollectFiles(serverDir, "server", result.IncludedFiles, new[] { "bin", "obj", ".vs" }, stagingDir);
                    result.Steps.Add($"  后端文件: {result.IncludedFiles.Count} 个");
                }

                // 清洗暂存区中的配置文件值（清除敏感数据，保持键和结构不变）
                // 仅在 sanitizeConfig=true 时执行；为 false 时保留所有真实配置值（用于内部备份等场景）
                if (sanitizeConfig)
                {
                    var sanitizedCount = await SanitizeConfigFilesAsync(stagingDir, ct);
                    if (sanitizedCount > 0)
                        result.Steps.Add($"  配置文件脱敏: {sanitizedCount} 个文件已清空真实值");
                }
                else
                {
                    result.Steps.Add("  配置文件脱敏: 跳过（用户选择保留真实值）");
                }
            }

            // ============================================================
            // 步骤 1.5: 收集契约层文件（contracts/）
            //   - 源码包：拷贝 contracts/ 下全部源码
            //   - 编译包：跳过（Contracts DLL 已随 server publish 一并产出于 server/bin/）
            // ============================================================
            result.Steps.Add("[1.5/10] 收集契约层文件...");
            var contractsDir = Path.Combine(moduleDir, "contracts");
            if (Directory.Exists(contractsDir))
            {
                if (isCompiled)
                {
                    result.Steps.Add("  契约层文件: 跳过（编译包下 Contracts DLL 已随 server publish 产出于 server/bin/）");
                }
                else
                {
                    var beforeCount = result.IncludedFiles.Count;
                    CollectFiles(contractsDir, "contracts", result.IncludedFiles, new[] { "bin", "obj", ".vs" }, stagingDir);
                    var contractsCount = result.IncludedFiles.Count - beforeCount;
                    result.Steps.Add($"  契约层文件: {contractsCount} 个");
                }
            }
            else
            {
                result.Steps.Add("  契约层文件: 跳过（不存在）");
            }

            // ============================================================
            // 步骤 2: 收集模块自带的 web/ 目录
            // ============================================================
            result.Steps.Add("[2/10] 收集模块Web文件...");
            var modWebDir = Path.Combine(moduleDir, "web");
            if (Directory.Exists(modWebDir))
            {
                CollectFiles(modWebDir, "web", result.IncludedFiles, new[] { "node_modules", "dist", ".cache" }, stagingDir);
                result.Steps.Add("  模块Web文件: OK");
            }
            else
            {
                result.Steps.Add("  模块Web文件: 跳过（不存在）");
            }

            // ============================================================
            // 步骤 3: 收集模块自带的 uniapp/ 目录
            // ============================================================
            result.Steps.Add("[3/10] 收集模块UniApp文件...");
            var modUniDir = Path.Combine(moduleDir, "uniapp");
            if (Directory.Exists(modUniDir))
            {
                CollectFiles(modUniDir, "uniapp", result.IncludedFiles, new[] { "node_modules", "dist", ".cache" }, stagingDir);
                result.Steps.Add("  模块UniApp文件: OK");
            }
            else
            {
                result.Steps.Add("  模块UniApp文件: 跳过（不存在）");
            }

            // ============================================================
            // 步骤 4: 收集模块自带的 client/ 目录（WPF客户端）
            //   - 源码包：拷贝 client/ 下全部源码
            //   - 编译包：dotnet publish client csproj → 仅拷贝 DLL 到 client/bin/Release/net8.0-windows/，
            //            使 WPF 端 DevModuleBootstrap 可通过 "client\bin\" 规则匹配加载
            // ============================================================
            result.Steps.Add("[4/10] 收集WPF客户端文件...");
            var modClientDir = Path.Combine(moduleDir, "client");
            if (Directory.Exists(modClientDir))
            {
                if (isCompiled)
                {
                    await CollectWpfCompiledAsync(modClientDir, "client", stagingDir, result, ct);
                }
                else
                {
                    CollectFiles(modClientDir, "client", result.IncludedFiles, new[] { "bin", "obj", ".vs" }, stagingDir);
                    result.Steps.Add("  WPF客户端文件: OK");
                }
            }
            else
            {
                result.Steps.Add("  WPF客户端文件: 跳过（不存在）");
            }

            // ============================================================
            // 步骤 5: Web 前端插件（自动发现）
            // ============================================================
            result.Steps.Add("[5/10] Web前端插件...");
            string? fePluginName = null;
            if (repoRoot != null)
            {
                var fePluginDir = FindWebPluginDir(moduleId, repoRoot);
                if (fePluginDir != null)
                {
                    fePluginName = Path.GetFileName(fePluginDir);
                    CollectFiles(fePluginDir, "web-plugin", result.IncludedFiles, new[] { "node_modules", "dist", ".cache" }, stagingDir);
                    result.Steps.Add($"  Web前端插件: {fePluginName}");
                }
                else
                {
                    result.Steps.Add("  Web前端插件: 未找到");
                }
            }

            // ============================================================
            // 步骤 6: UniApp 插件（自动发现）
            // ============================================================
            result.Steps.Add("[6/10] UniApp插件...");
            string? uniPluginName = null;
            if (repoRoot != null)
            {
                var uniPluginDir = FindUniappPluginDir(moduleId, repoRoot);
                if (uniPluginDir != null)
                {
                    uniPluginName = Path.GetFileName(uniPluginDir);
                    CollectFiles(uniPluginDir, "uniapp-plugin", result.IncludedFiles, new[] { "node_modules", "dist", ".cache" }, stagingDir);
                    result.Steps.Add($"  UniApp插件: {uniPluginName}");
                }
                else
                {
                    result.Steps.Add("  UniApp插件: 未找到");
                }
            }

            // ============================================================
            // 步骤 7: WPF 客户端插件（自动发现 src/Client 中的模块项目）
            //   - 源码包：拷贝整个目录源码
            //   - 编译包：dotnet publish → 仅拷贝 DLL 到 wpf-plugin/bin/Release/net8.0-windows/
            // ============================================================
            result.Steps.Add("[7/10] WPF客户端插件...");
            string? wpfPluginName = null;
            if (repoRoot != null)
            {
                var wpfPluginDir = FindWpfPluginDir(moduleId, repoRoot);
                if (wpfPluginDir != null)
                {
                    wpfPluginName = Path.GetFileName(wpfPluginDir);
                    if (isCompiled)
                    {
                        await CollectWpfCompiledAsync(wpfPluginDir, "wpf-plugin", stagingDir, result, ct);
                        result.Steps.Add($"  WPF客户端插件: {wpfPluginName}（编译产物）");
                    }
                    else
                    {
                        CollectFiles(wpfPluginDir, "wpf-plugin", result.IncludedFiles, new[] { "bin", "obj", ".vs" }, stagingDir);
                        result.Steps.Add($"  WPF客户端插件: {wpfPluginName}");
                    }
                }
                else
                {
                    result.Steps.Add("  WPF客户端插件: 未找到");
                }
            }

            // ============================================================
            // 步骤 8: 数据库导出（表结构 + 可选数据 + 菜单同步）
            // ============================================================
            result.Steps.Add("[8/10] 数据库导出...");
            await ExportDatabaseAsync(moduleDir, moduleId, stagingDir, exportDbSchema, exportDbData, manifest, result, progress, ct);

            // ============================================================
            // 步骤 9: 生成 install-manifest.json
            // ============================================================
            result.Steps.Add("[9/10] 生成安装清单...");
            var installManifest = new JsonObject
            {
                ["id"] = moduleId,
                ["version"] = version,
                ["packageType"] = packageType,
                ["installPaths"] = new JsonObject
                {
                    ["server"] = $"src/Module/{moduleId}/server/",
                    ["contracts"] = $"src/Module/{moduleId}/contracts/",
                    ["web"] = $"src/Module/{moduleId}/web/",
                    ["uniapp"] = $"src/Module/{moduleId}/uniapp/",
                    ["client"] = $"src/Module/{moduleId}/client/"
                }
            };
            if (fePluginName != null)
            {
                (installManifest["installPaths"] as JsonObject)!["web-plugin"] = $"web/src/plugins/installed/{fePluginName}/";
            }
            if (uniPluginName != null)
            {
                (installManifest["installPaths"] as JsonObject)!["uniapp-plugin"] = $"uniapp/pgzx/pages/plugins/{uniPluginName}/";
            }
            if (wpfPluginName != null)
            {
                (installManifest["installPaths"] as JsonObject)!["wpf-plugin"] = $"src/Client/{wpfPluginName}/";
            }
            var manifestPath = Path.Combine(stagingDir, "install-manifest.json");
            var manifestJson = JsonSerializer.Serialize(installManifest, CreateReadableJsonOptions());
            await File.WriteAllTextAsync(manifestPath, manifestJson, System.Text.Encoding.UTF8, ct);
            result.IncludedFiles.Add("install-manifest.json");

            // 复制根目录配置文件
            foreach (var rootFile in new[] { "README.md", "LICENSE", "CHANGELOG.md" })
            {
                var filePath = Path.Combine(moduleDir, rootFile);
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, Path.Combine(stagingDir, rootFile), true);
                    result.IncludedFiles.Add(rootFile);
                }
            }

            // ============================================================
            // 步骤 9.5: 清理安装器不允许的敏感文件（证书/密钥等）
            // 与 ModuleUploadService.ValidateExtractedPaths 白名单保持一致，避免打入 .pem 等导致安装被拒
            // ============================================================
            result.Steps.Add("[9.5/10] 清理不允许打入模块包的敏感文件...");
            var removedSensitive = PurgeDisallowedPackageFiles(stagingDir, result.IncludedFiles);
            if (removedSensitive.Count > 0)
                result.Steps.Add($"  已排除 {removedSensitive.Count} 个文件：{string.Join(", ", removedSensitive)}");
            else
                result.Steps.Add("  未发现需排除的敏感文件");

            // ============================================================
            // 步骤 10: 计算文件哈希并写入 module.json
            // ============================================================
            result.Steps.Add("[10] 计算文件哈希...");
            var stagedModuleJson = Path.Combine(stagingDir, "server", "module.json");
            if (File.Exists(stagedModuleJson))
            {
                var hashCount = await ComputeAndWriteFileHashesAsync(stagingDir, stagedModuleJson, ct);
                result.Steps.Add($"  已计算 {hashCount} 个文件的 SHA256 哈希");
            }

            // 创建 ZIP 包（输出到 dist/modules）
            var outputDir = GetPackageOutputDir();
            var typeSuffix = packageType == "compiled" ? "compiled" : "source";
            var fileName = $"{moduleId}_v{version}_{typeSuffix}.gmod.zip";
            var packagePath = Path.Combine(outputDir, fileName);

            // 打包前列出暂存目录中所有 SQL 文件（用于排查丢失问题）
            var allStagedFiles = Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories);
            result.Steps.Add($"  暂存目录共 {allStagedFiles.Length} 个文件");
            var sqlFiles = allStagedFiles.Where(f => f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var sf in sqlFiles)
            {
                var relPath = Path.GetRelativePath(stagingDir, sf).Replace('\\', '/');
                var size = new FileInfo(sf).Length;
                result.Steps.Add($"  SQL文件: {relPath}（{size} bytes）");
            }

            // 删除已存在的包
            if (File.Exists(packagePath))
                File.Delete(packagePath);

            ZipFile.CreateFromDirectory(stagingDir, packagePath, CompressionLevel.Optimal, false, System.Text.Encoding.UTF8);

            // 清理暂存目录
            try { Directory.Delete(stagingDir, true); } catch { }

            // 获取文件大小
            var fileInfo = new FileInfo(packagePath);

            result.Ok = true;
            result.Message = "打包成功";
            result.PackagePath = packagePath;
            result.FileName = fileName;
            result.FileSize = fileInfo.Length;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打包模块 {ModuleId} 失败", moduleId);
            result.Message = $"打包失败: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 导出数据库（三种模式：源文件直接复制 / 真实结构导出 / 真实结构+数据导出）
    /// </summary>
    private async Task ExportDatabaseAsync(string moduleDir, string moduleId, string stagingDir, bool exportDbSchema, bool exportDbData, ModuleManifest manifest, ModulePackageResult result, IProgress<string>? progress, CancellationToken ct)
    {
        void Step(string msg)
        {
            result.Steps.Add(msg);
            progress?.Report(msg);
        }

        var serverDir = Path.Combine(moduleDir, "server");

        if (!exportDbSchema)
        {
            // 模式 A：不导出数据库 — SQL 文件已由 CollectFiles 递归收集到暂存区，无需额外处理
            Step("  数据库导出: 跳过（使用源码中的 SQL 文件）");
        }
        else
        {
            // 模式 B / C：从真实数据库导出表结构（及可选数据）
            try
            {
                // 1. 确定要导出的表名列表
                var tableNames = new List<string>();
                var tablePrefix = manifest.TablePrefix;

                if (!string.IsNullOrWhiteSpace(tablePrefix))
                {
                    // 按前缀从数据库中查找所有匹配的表
                    tableNames = await _sqlExecutor.FindTablesByPrefixAsync(tablePrefix, ct);
                    Step($"  按前缀 [{tablePrefix}] 查找表: {tableNames.Count} 张");
                }
                else
                {
                    // 回退：从源码中的 install.sql 提取表名
                    var installSqlFiles = FindInstallSqlFiles(serverDir);
                    foreach (var sqlFile in installSqlFiles)
                    {
                        var sqlContent = await File.ReadAllTextAsync(sqlFile, ct);
                        var names = ModuleSqlExecutor.ExtractTableNamesFromSql(sqlContent);
                        tableNames.AddRange(names);
                    }
                    // 去重
                    tableNames = tableNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (tableNames.Count > 0)
                        Step($"  从 install.sql 提取表名: {tableNames.Count} 张（未配置 tablePrefix，使用回退策略）");
                    else
                        Step("  数据库导出: 跳过（未配置 tablePrefix 且未找到 install.sql）");
                }

                if (tableNames.Count > 0)
                {
                    tableNames = await _sqlExecutor.SortTablesForDataExportAsync(tableNames, ct);
                    Step($"  表顺序: 已按外键依赖排序（{tableNames.Count} 张）");

                    // 2. 导出表结构
                    var schemaSql = await _sqlExecutor.ExportTableSchemaAsync(tableNames, ct);
                    if (!string.IsNullOrWhiteSpace(schemaSql))
                    {
                        // 确定写入路径：保持与源文件相同的相对路径
                        var installSqlRelPath = DetermineInstallSqlRelativePath(serverDir);
                        var targetPath = Path.Combine(stagingDir, "server", installSqlRelPath);
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (targetDir != null && !Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);
                        await File.WriteAllTextAsync(targetPath, schemaSql, System.Text.Encoding.UTF8, ct);
                        Step($"  导出表结构: {tableNames.Count} 张表 → {installSqlRelPath}");
                    }

                    // 3. 导出表数据（模式 C：全量流式写入，支持百万级）
                    if (exportDbData)
                    {
                        Step($"  开始全量导出表数据（{tableNames.Count} 张表，数据量大时可能耗时较长，请耐心等待）...");

                        var installSqlRelPath = DetermineInstallSqlRelativePath(serverDir);
                        var sqlDir = Path.GetDirectoryName(installSqlRelPath) ?? "sql";
                        var dataRelPath = Path.Combine(sqlDir, "init_data.sql");
                        var dataTargetPath = Path.Combine(stagingDir, "server", dataRelPath);
                        var dataTargetDir = Path.GetDirectoryName(dataTargetPath);
                        if (dataTargetDir != null && !Directory.Exists(dataTargetDir))
                            Directory.CreateDirectory(dataTargetDir);

                        var exportStats = await _sqlExecutor.ExportTableDataToFileAsync(
                            tableNames,
                            dataTargetPath,
                            ct,
                            rowLimit: null,
                            onProgress: Step);

                        if (exportStats.TotalRows > 0 || exportStats.TotalBytes > 0)
                        {
                            result.IncludedFiles.Add($"server/{dataRelPath.Replace('\\', '/')}");

                            _logger.LogInformation(
                                "init_data.sql 已写入: {Path}, 行数: {Rows}, 大小: {Size} bytes",
                                dataTargetPath, exportStats.TotalRows, exportStats.TotalBytes);
                            Step($"  导出表数据: {exportStats.TotalRows:N0} 行 → {dataRelPath}（{exportStats.TotalBytes:N0} bytes）");

                            // 立即将 init_data.sql 注册到暂存区 install.json 的 SqlScripts 中（不依赖后续文件检测）
                            var dataRelPathNormalized = dataRelPath.Replace('\\', '/');
                            await RegisterSqlScriptInInstallJsonAsync(stagingDir, serverDir, dataRelPathNormalized, ct);
                            Step($"  已注册 SqlScript: {dataRelPathNormalized}");
                        }
                        else
                        {
                            Step("  导出表数据: 无数据行（已跳过 init_data.sql 注册）");
                            if (File.Exists(dataTargetPath))
                                File.Delete(dataTargetPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "导出数据库表结构/数据失败");
                Step($"  导出数据库失败: {ex.Message}");
            }
        }

        // 菜单同步始终执行
        await SyncMenusToInstallJsonAsync(serverDir, stagingDir, result, ct);
    }

    /// <summary>
    /// 将指定的 SQL 脚本路径注册到暂存区 install.json 的 SqlScripts 数组中
    /// </summary>
    private async Task RegisterSqlScriptInInstallJsonAsync(string stagingDir, string serverDir, string sqlRelPath, CancellationToken ct)
    {
        var installJsonPath = Path.Combine(stagingDir, "server", "install.json");

        // 如果暂存区没有 install.json，从源目录复制一份
        if (!File.Exists(installJsonPath))
        {
            var srcInstallJson = Path.Combine(serverDir, "install.json");
            if (!File.Exists(srcInstallJson))
            {
                _logger.LogWarning("无法注册 SqlScript：install.json 不存在");
                return;
            }
            var targetDir = Path.GetDirectoryName(installJsonPath);
            if (targetDir != null && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);
            File.Copy(srcInstallJson, installJsonPath, true);
        }

        var json = await File.ReadAllTextAsync(installJsonPath, ct);
        var node = JsonNode.Parse(json);
        if (node == null) return;

        var scripts = node["SqlScripts"] as JsonArray;
        if (scripts == null)
        {
            scripts = new JsonArray();
            node["SqlScripts"] = scripts;
        }

        // 避免重复添加
        bool alreadyHas = false;
        foreach (var s in scripts)
        {
            if (string.Equals(s?.GetValue<string>(), sqlRelPath, StringComparison.OrdinalIgnoreCase))
            {
                alreadyHas = true;
                break;
            }
        }

        if (!alreadyHas)
        {
            scripts.Add(sqlRelPath);
            _logger.LogInformation("已将 {Path} 添加到 install.json SqlScripts", sqlRelPath);
        }

        await File.WriteAllTextAsync(installJsonPath, JsonSerializer.Serialize(node, CreateReadableJsonOptions()), System.Text.Encoding.UTF8, ct);
    }

    /// <summary>
    /// 查找模块 server/sql 目录下所有 install.sql 文件（兼容根目录和数据库类型子目录两种风格）
    /// </summary>
    private static List<string> FindInstallSqlFiles(string serverDir)
    {
        var result = new List<string>();
        var sqlDir = Path.Combine(serverDir, "sql");
        if (!Directory.Exists(sqlDir))
            return result;

        // 优先查找根目录下的 install.sql
        var rootInstall = Path.Combine(sqlDir, "install.sql");
        if (File.Exists(rootInstall))
        {
            result.Add(rootInstall);
            return result;
        }

        // 回退：查找子目录下的 install.sql（如 sql/mysql/install.sql、sql/mssql/install.sql）
        foreach (var subDir in Directory.GetDirectories(sqlDir))
        {
            var subInstall = Path.Combine(subDir, "install.sql");
            if (File.Exists(subInstall))
                result.Add(subInstall);
        }

        return result;
    }

    /// <summary>
    /// 确定 install.sql 的相对路径（相对于 server/ 目录），用于导出时保持原始目录结构
    /// </summary>
    private static string DetermineInstallSqlRelativePath(string serverDir)
    {
        var sqlDir = Path.Combine(serverDir, "sql");

        // 优先使用根目录下的 install.sql 路径
        var rootInstall = Path.Combine(sqlDir, "install.sql");
        if (File.Exists(rootInstall))
            return Path.Combine("sql", "install.sql");

        // 查找子目录（如 sql/mysql/install.sql）
        if (Directory.Exists(sqlDir))
        {
            foreach (var subDir in Directory.GetDirectories(sqlDir))
            {
                var subInstall = Path.Combine(subDir, "install.sql");
                if (File.Exists(subInstall))
                    return Path.Combine("sql", Path.GetFileName(subDir), "install.sql");
            }
        }

        // 默认路径
        return Path.Combine("sql", "install.sql");
    }

    /// <summary>
    /// 从数据库导出最新菜单树，更新 install.json 中的 Menus 节
    /// </summary>
    private async Task SyncMenusToInstallJsonAsync(string serverDir, string stagingDir, ModulePackageResult result, CancellationToken ct)
    {
        var installJsonPath = Path.Combine(stagingDir, "server", "install.json");
        if (!File.Exists(installJsonPath))
        {
            // 尝试从源目录复制
            var srcInstallJson = Path.Combine(serverDir, "install.json");
            if (File.Exists(srcInstallJson))
            {
                var targetDir = Path.Combine(stagingDir, "server");
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);
                File.Copy(srcInstallJson, installJsonPath, true);
            }
            else
            {
                result.Steps.Add("  菜单同步: 跳过（无 install.json）");
                return;
            }
        }

        try
        {
            var installJson = await File.ReadAllTextAsync(installJsonPath, ct);
            var installNode = JsonNode.Parse(installJson);
            if (installNode == null)
            {
                result.Steps.Add("  菜单同步: 跳过（install.json 解析失败）");
                return;
            }

            // 提取 RootCode
            var rootCode = installNode["Menus"]?["RootCode"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(rootCode))
            {
                result.Steps.Add("  菜单同步: 跳过（未定义 RootCode）");
                return;
            }

            // 从数据库导出菜单树
            var exportedSpec = await _sqlExecutor.ExportMenuTreeAsync(rootCode, ct);
            if (exportedSpec?.Menus == null)
            {
                result.Steps.Add($"  菜单同步: 跳过（数据库中未找到根菜单 {rootCode}）");
                return;
            }

            // 更新 install.json 中的 Menus 节（保留 RootId 等原有字段）
            var menusNode = installNode["Menus"];
            if (menusNode != null)
            {
                // 更新根菜单信息
                if (exportedSpec.Menus.RootName != null)
                    menusNode["RootName"] = exportedSpec.Menus.RootName;
                if (exportedSpec.Menus.RootIcon != null)
                    menusNode["RootIcon"] = exportedSpec.Menus.RootIcon;
                if (exportedSpec.Menus.RootSupportedClients != null)
                    menusNode["RootSupportedClients"] = exportedSpec.Menus.RootSupportedClients;

                // 更新子菜单项
                if (exportedSpec.Menus.Items != null && exportedSpec.Menus.Items.Count > 0)
                {
                    var itemsArray = new JsonArray();
                    foreach (var item in exportedSpec.Menus.Items)
                    {
                        var itemNode = new JsonObject
                        {
                            ["Name"] = item.Name,
                            ["Route"] = item.Route,
                            ["Type"] = item.Type
                        };
                        if (item.Code != null) itemNode["Code"] = item.Code;
                        if (item.Icon != null) itemNode["Icon"] = item.Icon;
                        if (item.ParentCode != null) itemNode["ParentCode"] = item.ParentCode;
                        if (item.WebRouteUrl != null) itemNode["WebRouteUrl"] = item.WebRouteUrl;
                        if (item.WebDisplayMode != null) itemNode["WebDisplayMode"] = item.WebDisplayMode;
                        if (item.SupportedClients != null) itemNode["SupportedClients"] = item.SupportedClients;
                        if (item.Url != null) itemNode["Url"] = item.Url;
                        if (item.Resource != null) itemNode["Resource"] = item.Resource;
                        if (item.Method != null) itemNode["Method"] = item.Method;
                        if (item.ItemMode != null) itemNode["ItemMode"] = item.ItemMode;
                        itemNode["SortOrder"] = item.SortOrder;
                        itemNode["Hidden"] = item.Hidden;
                        itemsArray.Add(itemNode);
                    }
                    menusNode["Items"] = itemsArray;
                }
            }

            // 写回 install.json（SqlScripts 已在 ExportDatabaseAsync 中由 RegisterSqlScriptInInstallJsonAsync 处理）
            var formattedJson = JsonSerializer.Serialize(installNode, CreateReadableJsonOptions());
            await File.WriteAllTextAsync(installJsonPath, formattedJson, System.Text.Encoding.UTF8, ct);
            result.Steps.Add($"  菜单同步: OK（{exportedSpec.Menus.Items?.Count ?? 0} 个菜单项）");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "同步菜单数据到 install.json 失败");
            result.Steps.Add($"  菜单同步失败: {ex.Message}");
        }
    }

    // ============================================================
    // Web 前端插件 / UniApp 插件自动发现
    // ============================================================

    /// <summary>
    /// 自动发现 Web 前端插件目录
    /// </summary>
    private string? FindWebPluginDir(string moduleId, string repoRoot)
    {
        var pluginsBase = Path.Combine(repoRoot, "web", "src", "plugins", "installed");
        if (!Directory.Exists(pluginsBase)) return null;

        // 1. 扫描各插件目录下的 module.json 查找 moduleId 匹配
        foreach (var dir in Directory.GetDirectories(pluginsBase))
        {
            var mjPath = Path.Combine(dir, "module.json");
            if (File.Exists(mjPath))
            {
                try
                {
                    var json = File.ReadAllText(mjPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("moduleId", out var mid) && mid.GetString() == moduleId)
                        return dir;
                }
                catch { }
            }
        }

        // 2. 回退：基于模块名称查找（去掉 Ginkgo.Module. 前缀后转小写，例如 ResourceMonitor → resourcemonitor）
        var rawShortName = moduleId.Replace("Ginkgo.Module.", "", StringComparison.OrdinalIgnoreCase);
        var shortName = rawShortName.ToLowerInvariant();
        var fallbackDir = Path.Combine(pluginsBase, shortName);
        if (Directory.Exists(fallbackDir)) return fallbackDir;

        // 3. 回退：PascalCase → kebab-case（例如 ResourceMonitor → resource-monitor）
        //    注意：必须用未转小写的 rawShortName 进行转换，才能识别大写边界
        var kebabName = PascalToKebab(rawShortName);
        if (!string.Equals(kebabName, shortName, StringComparison.Ordinal))
        {
            var kebabDir = Path.Combine(pluginsBase, kebabName);
            if (Directory.Exists(kebabDir)) return kebabDir;
        }

        return null;
    }

    /// <summary>
    /// 将 PascalCase 字符串转换为 kebab-case（例如 ResourceMonitor → resource-monitor）
    /// </summary>
    private static string PascalToKebab(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return System.Text.RegularExpressions.Regex.Replace(input, @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }

    /// <summary>
    /// 自动发现 UniApp 插件目录
    /// </summary>
    private string? FindUniappPluginDir(string moduleId, string repoRoot)
    {
        var pluginsBase = Path.Combine(repoRoot, "uniapp", "pgzx", "pages", "plugins");
        if (!Directory.Exists(pluginsBase)) return null;

        var shortName = moduleId.Replace("Ginkgo.Module.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var dir = Path.Combine(pluginsBase, shortName);
        if (Directory.Exists(dir)) return dir;

        return null;
    }

    /// <summary>
    /// 自动发现 WPF 客户端插件目录（在 src/Client 下查找包含模块名称的项目目录）
    /// </summary>
    private string? FindWpfPluginDir(string moduleId, string repoRoot)
    {
        var clientBase = Path.Combine(repoRoot, "src", "Client");
        if (!Directory.Exists(clientBase)) return null;

        // 1. 精确匹配：查找名称包含模块短名（去掉 Ginkgo.Module. 前缀）的 WPF 项目目录
        var shortName = moduleId.Replace("Ginkgo.Module.", "", StringComparison.OrdinalIgnoreCase);

        foreach (var dir in Directory.GetDirectories(clientBase))
        {
            var dirName = Path.GetFileName(dir);
            // 排除框架核心 WPF 项目（Ginkgo.Wpf、Ginkgo.UI、Ginkgo.Wpf.Module.Abstractions）
            if (dirName.Equals("Ginkgo.Wpf", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Ginkgo.UI", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Ginkgo.Wpf.Module.Abstractions", StringComparison.OrdinalIgnoreCase))
                continue;

            // 匹配包含模块短名的项目目录
            if (dirName.Contains(shortName, StringComparison.OrdinalIgnoreCase))
                return dir;
        }

        // 2. 回退：查找与模块 ID 同名的目录（如 Ginkgo.Module.xxx 本身）
        var fallbackDir = Path.Combine(clientBase, moduleId);
        if (Directory.Exists(fallbackDir)) return fallbackDir;

        return null;
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>
    /// 查找模块目录
    /// </summary>
    private string? FindModuleDirectory(string moduleId)
    {
        _logger.LogDebug("查找模块目录: {ModuleId}", moduleId);
        
        // 开发环境：在 src/Module 目录查找
        var devDir = GetDevModuleSourceDir();
        _logger.LogDebug("开发模块目录: {DevDir}", devDir ?? "(null)");
        
        if (devDir != null)
        {
            var moduleDir = Path.Combine(devDir, moduleId);
            _logger.LogDebug("尝试模块路径: {ModuleDir}, 存在: {Exists}", moduleDir, Directory.Exists(moduleDir));
            if (Directory.Exists(moduleDir))
                return moduleDir;
        }

        // 生产环境：在 modules 目录查找
        var prodDir = Path.Combine(AppContext.BaseDirectory, "modules", moduleId);
        if (Directory.Exists(prodDir))
        {
            // 查找最新版本目录
            var versionDirs = Directory.GetDirectories(prodDir);
            var latestVersion = versionDirs.OrderByDescending(d => d).FirstOrDefault();
            if (latestVersion != null)
                return latestVersion;
        }

        return null;
    }

    /// <summary>
    /// 在目录中查找文件
    /// </summary>
    private string? FindFile(string directory, string fileName)
    {
        var rootFile = Path.Combine(directory, fileName);
        if (File.Exists(rootFile))
            return rootFile;

        var serverFile = Path.Combine(directory, "server", fileName);
        if (File.Exists(serverFile))
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

    /// <summary>
    /// 计算暂存目录中所有文件的 SHA256 哈希并写入 module.json 的 files 字段
    /// 安装端会通过 ModuleHashValidator 校验这些哈希
    /// </summary>
    private async Task<int> ComputeAndWriteFileHashesAsync(string stagingDir, string moduleJsonPath, CancellationToken ct)
    {
        var moduleJsonText = await File.ReadAllTextAsync(moduleJsonPath, ct);
        var moduleJsonNode = JsonNode.Parse(moduleJsonText) ?? new JsonObject();

        var filesArray = new JsonArray();
        var allFiles = Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories);
        var count = 0;

        foreach (var file in allFiles)
        {
            // 跳过 module.json 自身（因为写入哈希后文件内容会变）
            if (Path.GetFullPath(file).Equals(Path.GetFullPath(moduleJsonPath), StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(stagingDir, file).Replace('\\', '/');
            var hash = await ModuleHashValidator.ComputeSha256Async(file, ct);
            filesArray.Add(new JsonObject
            {
                ["path"] = relativePath,
                ["sha256"] = hash
            });
            count++;
        }

        moduleJsonNode["files"] = filesArray;

        var options = CreateReadableJsonOptions();
        var updatedJson = moduleJsonNode.ToJsonString(options);
        await File.WriteAllTextAsync(moduleJsonPath, updatedJson, System.Text.Encoding.UTF8, ct);

        return count;
    }

    /// <summary>
    /// 【编译包】收集后端文件：
    ///  1) 调用 dotnet publish 将 server 下的 *.csproj 发布到临时目录
    ///  2) 将发布产物（*.dll/*.pdb/*.xml/*.deps.json/*.runtimeconfig.json 以及依赖 DLL）拷贝到 stagingDir/server/bin/
    ///  3) 将 server/ 下的非源码资源（module.json/install.json/sql/config/data）拷贝到 stagingDir/server/
    ///  —— 产出的 zip 内不包含任何 .cs/.csproj 等源码
    /// </summary>
    private async Task CollectServerCompiledAsync(string serverDir, string stagingDir, ModulePackageResult result, CancellationToken ct)
    {
        // 1) 定位 server 的 csproj
        var csproj = Directory.GetFiles(serverDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (csproj == null)
        {
            // 没有 csproj 的纯资源型后端（极少见）：退化为拷贝全部非源码
            result.Steps.Add("  后端: 未找到 .csproj，按资源目录原样拷贝");
            CollectNonSourceAssets(serverDir, "server", stagingDir, result);
            return;
        }

        // 2) publish 到临时目录
        var publishOut = Path.Combine(Path.GetTempPath(), "ginkgo_publish", Guid.NewGuid().ToString("N"));
        try
        {
            result.Steps.Add($"  执行 dotnet publish (Release) → {publishOut}");
            var pr = await _dotnetBuild.PublishAsync(csproj, publishOut, "Release", ct);
            if (!pr.Ok)
            {
                var tail = (pr.StdErr ?? string.Empty);
                if (tail.Length == 0) tail = pr.StdOut ?? string.Empty;
                if (tail.Length > 600) tail = tail.Substring(tail.Length - 600);
                throw new InvalidOperationException($"后端 publish 失败（{pr.ElapsedMs} ms）：{pr.Message} {tail}");
            }
            result.Steps.Add($"  后端 publish 成功（{pr.ElapsedMs} ms）");

            // 3) 拷贝产物到 stagingDir/server/bin/
            var stagedBinDir = Path.Combine(stagingDir, "server", "bin");
            Directory.CreateDirectory(stagedBinDir);
            var copied = CopyAllFilesFlat(publishOut, stagedBinDir, stagingDir, "server/bin", result.IncludedFiles);
            result.Steps.Add($"  后端产物: {copied} 个文件 → server/bin/");
        }
        finally
        {
            try { if (Directory.Exists(publishOut)) Directory.Delete(publishOut, true); } catch { }
        }

        // 4) 拷贝 server/ 下的非源码资源到 stagingDir/server/
        CollectNonSourceAssets(serverDir, "server", stagingDir, result);

        // 5) 修正 stagingDir/server/module.json 的 entryAssembly 字段：
        //    运行期 ScanProductionModules 以 module.json 所在目录为基准解析 entryAssembly，
        //    module.json 在 server/，DLL 在 server/bin/，所以 entryAssembly 应为 "bin/<AssemblyName>.dll"。
        try
        {
            var stagedModuleJson = Path.Combine(stagingDir, "server", "module.json");
            if (File.Exists(stagedModuleJson))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(csproj);
                var json = await File.ReadAllTextAsync(stagedModuleJson, ct);
                var node = JsonNode.Parse(json);
                if (node != null)
                {
                    var srv = node["server"] as JsonObject;
                    if (srv == null)
                    {
                        srv = new JsonObject();
                        node["server"] = srv;
                    }
                    var newEntry = $"bin/{assemblyName}.dll";
                    srv["entryAssembly"] = newEntry;
                    await File.WriteAllTextAsync(stagedModuleJson, JsonSerializer.Serialize(node, CreateReadableJsonOptions()), System.Text.Encoding.UTF8, ct);
                    result.Steps.Add($"  module.json entryAssembly → {newEntry}（相对 server/）");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 module.json entryAssembly 失败");
            result.Steps.Add($"  更新 module.json entryAssembly 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 【编译包】收集 WPF 客户端文件（模块自带 client/ 或独立的 wpf-plugin 目录）：
    ///  1) dotnet publish 到临时目录
    ///  2) 将 DLL/PDB/XML 拷贝到 stagingDir/&lt;relDir&gt;/bin/Release/net8.0-windows/
    ///     （这个相对路径同时满足 WPF 端 DevModuleBootstrap 的 "client\bin\" / "bin\Release\" 匹配规则）
    ///  3) 拷贝该目录下的非源码资源（如 manifest.json / Resources/）到 stagingDir/&lt;relDir&gt;/
    /// </summary>
    private async Task CollectWpfCompiledAsync(string sourceDir, string relDir, string stagingDir, ModulePackageResult result, CancellationToken ct)
    {
        var csproj = Directory.GetFiles(sourceDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (csproj == null)
        {
            result.Steps.Add($"  {relDir}: 未找到 .csproj，按资源目录原样拷贝");
            CollectNonSourceAssets(sourceDir, relDir, stagingDir, result);
            return;
        }

        var publishOut = Path.Combine(Path.GetTempPath(), "ginkgo_publish", Guid.NewGuid().ToString("N"));
        try
        {
            result.Steps.Add($"  执行 dotnet publish (Release) → {publishOut}");
            var pr = await _dotnetBuild.PublishAsync(csproj, publishOut, "Release", ct);
            if (!pr.Ok)
            {
                var tail = (pr.StdErr ?? string.Empty);
                if (tail.Length == 0) tail = pr.StdOut ?? string.Empty;
                if (tail.Length > 600) tail = tail.Substring(tail.Length - 600);
                throw new InvalidOperationException($"{relDir} publish 失败（{pr.ElapsedMs} ms）：{pr.Message} {tail}");
            }
            result.Steps.Add($"  {relDir} publish 成功（{pr.ElapsedMs} ms）");

            // 目标相对路径：<relDir>/bin/Release/net8.0-windows/
            // 选择这个子目录是为了兼容 Ginkgo.Wpf.Modules.DevModuleBootstrap 里的匹配规则（需包含 client/bin 或 bin/Release 子串）
            var rel = Path.Combine(relDir, "bin", "Release", "net8.0-windows");
            var stagedBinDir = Path.Combine(stagingDir, rel);
            Directory.CreateDirectory(stagedBinDir);
            var copied = CopyAllFilesFlat(publishOut, stagedBinDir, stagingDir, rel.Replace('\\', '/'), result.IncludedFiles);
            result.Steps.Add($"  {relDir} 产物: {copied} 个文件 → {rel.Replace('\\', '/')}/");
        }
        finally
        {
            try { if (Directory.Exists(publishOut)) Directory.Delete(publishOut, true); } catch { }
        }

        // 拷贝该目录下的非源码资源（manifest.json/Resources/等）到 stagingDir/<relDir>/
        CollectNonSourceAssets(sourceDir, relDir, stagingDir, result);
    }

    /// <summary>
    /// 安装器白名单不允许的扩展名（证书/密钥须部署侧自行配置，不可打入模块包）。
    /// 与 <see cref="ModuleUploadService"/> 解压校验白名单保持一致。
    /// </summary>
    private static readonly HashSet<string> PackageExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem", ".key", ".pfx", ".p12", ".crt", ".cer"
    };

    /// <summary>
    /// 判断相对路径是否属于打包时应整段排除的目录（如支付微信证书目录）。
    /// </summary>
    private static bool IsPackageExcludedRelativePath(string relativePath)
    {
        var norm = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(norm))
            return false;

        // 微信/支付证书目录：须目标环境在 server/config/wechatpay/ 自行放置，禁止随模块包分发
        return norm.Contains("/config/wechatpay/", StringComparison.OrdinalIgnoreCase)
            || norm.EndsWith("/config/wechatpay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(norm, "server/config/wechatpay", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断单个文件是否应被排除在模块包之外。
    /// </summary>
    private static bool ShouldExcludeFromPackage(string relativePath, string? extension)
    {
        if (IsPackageExcludedRelativePath(relativePath))
            return true;

        return !string.IsNullOrEmpty(extension) && PackageExcludedExtensions.Contains(extension);
    }

    /// <summary>
    /// 扫描暂存区并删除不允许安装的文件，同时从 IncludedFiles 中移除对应项。
    /// </summary>
    private static List<string> PurgeDisallowedPackageFiles(string stagingDir, List<string> includedFiles)
    {
        var removed = new List<string>();
        foreach (var file in Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(stagingDir, file).Replace('\\', '/');
            var ext = Path.GetExtension(file);
            if (!ShouldExcludeFromPackage(rel, ext))
                continue;

            File.Delete(file);
            removed.Add(rel);
            includedFiles.RemoveAll(f => string.Equals(f.Replace('\\', '/'), rel, StringComparison.OrdinalIgnoreCase));
        }

        return removed;
    }

    /// <summary>
    /// 收集目录下的「非源码资源」：递归复制除 .cs/.csproj/.sln/.user/.cache/bin/obj/.vs 之外的所有文件。
    /// 用于编译包模式保留 module.json/install.json/sql/config/data 等运行期必需内容。
    /// 【低成本反编译防御】同时排除 .pdb（调试符号）与 .xml（文档注释），即使用户源码目录下
    /// 偶然存在遗留的调试符号或文档，也不会被打入编译包。
    /// </summary>
    private void CollectNonSourceAssets(string sourceDir, string relativePath, string stagingDir, ModulePackageResult result)
    {
        var skipDirs = new HashSet<string>(new[] { "bin", "obj", ".vs", "test" }, StringComparer.OrdinalIgnoreCase);
        var skipExts = new HashSet<string>(new[] { ".cs", ".csproj", ".sln", ".user", ".cache", ".pdb", ".xml" }, StringComparer.OrdinalIgnoreCase);

        int count = 0;
        void Recurse(string dir, string rel)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                var fileName = Path.GetFileName(file);
                var ext = Path.GetExtension(fileName);
                if (skipExts.Contains(ext)) continue;
                if (fileName.EndsWith(".user", StringComparison.OrdinalIgnoreCase)) continue;

                var relFile = Path.Combine(rel, fileName);
                if (ShouldExcludeFromPackage(relFile.Replace('\\', '/'), ext))
                    continue;
                var target = Path.Combine(stagingDir, relFile);
                var targetDir = Path.GetDirectoryName(target);
                if (targetDir != null && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);
                File.Copy(file, target, true);
                result.IncludedFiles.Add(relFile.Replace('\\', '/'));
                count++;
            }
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var subName = Path.GetFileName(sub);
                if (skipDirs.Contains(subName)) continue;
                var nextRel = Path.Combine(rel, subName);
                if (IsPackageExcludedRelativePath(nextRel.Replace('\\', '/')))
                    continue;
                Recurse(sub, nextRel);
            }
        }

        if (Directory.Exists(sourceDir))
        {
            Recurse(sourceDir, relativePath);
            result.Steps.Add($"  {relativePath} 非源码资源: {count} 个文件");
        }
    }

    /// <summary>
    /// 将源目录下（含子目录）的所有文件「扁平化」拷贝到目标目录。
    /// 仅保留源目录第一层下子目录名（如 runtimes/、cs/ 这些 dotnet publish 的多语言或 native 目录）。
    /// 返回拷贝的文件数量。
    /// 【低成本反编译防御】始终跳过 .pdb（调试符号）与 .xml（XML 文档注释），避免将方法注释、
    /// 参数名与行号信息打入编译包，削弱 dnSpy / ILSpy 等反编译工具的可读性。
    /// 即使上游 publish 参数未生效（例如第三方依赖包自带 xml 文档），此处也能兜底过滤。
    /// </summary>
    private static int CopyAllFilesFlat(string sourceDir, string targetDir, string stagingDir, string relTargetDir, List<string> includedFiles)
    {
        int count = 0;
        void Recurse(string dir, string relPath)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                var fileName = Path.GetFileName(file);
                var ext = Path.GetExtension(fileName);
                // 跳过调试符号与 XML 文档（低成本反编译防御）
                if (string.Equals(ext, ".pdb", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var destRel = string.IsNullOrEmpty(relPath) ? fileName : Path.Combine(relPath, fileName);
                var stagedRel = (relTargetDir + "/" + destRel.Replace('\\', '/')).Replace("//", "/");
                if (ShouldExcludeFromPackage(stagedRel, ext))
                    continue;
                var destFull = Path.Combine(targetDir, destRel);
                var destFullDir = Path.GetDirectoryName(destFull);
                if (destFullDir != null && !Directory.Exists(destFullDir))
                    Directory.CreateDirectory(destFullDir);
                File.Copy(file, destFull, true);
                includedFiles.Add((relTargetDir + "/" + destRel.Replace('\\', '/')).Replace("//", "/"));
                count++;
            }
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var subName = Path.GetFileName(sub);
                Recurse(sub, string.IsNullOrEmpty(relPath) ? subName : Path.Combine(relPath, subName));
            }
        }
        Recurse(sourceDir, string.Empty);
        return count;
    }

    /// <summary>
    /// 收集目录中的文件到暂存目录
    /// </summary>
    private void CollectFiles(string sourceDir, string relativePath, List<string> files, string[] excludeDirs, string stagingDir)
    {
        var excludeSet = new HashSet<string>(excludeDirs, StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            // 排除临时文件
            if (fileName.EndsWith(".user") || fileName.EndsWith(".cache"))
                continue;

            var relPath = Path.Combine(relativePath, fileName);
            if (ShouldExcludeFromPackage(relPath.Replace('\\', '/'), Path.GetExtension(fileName)))
                continue;

            var targetPath = Path.Combine(stagingDir, relPath);
            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);
            File.Copy(file, targetPath, true);
            files.Add(relPath);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            if (excludeSet.Contains(dirName))
                continue;

            var nextRel = Path.Combine(relativePath, dirName);
            if (IsPackageExcludedRelativePath(nextRel.Replace('\\', '/')))
                continue;

            CollectFiles(dir, nextRel, files, excludeDirs, stagingDir);
        }
    }

    /// <summary>
    /// 清洗暂存区中的插件配置文件，清除所有 items[].value 中的值
    /// 仅操作暂存区副本，不影响源系统的原始配置文件
    /// </summary>
    /// <returns>清洗的文件数量</returns>
    private async Task<int> SanitizeConfigFilesAsync(string stagingDir, CancellationToken ct)
    {
        var configDir = Path.Combine(stagingDir, "server", "config");
        if (!Directory.Exists(configDir))
            return 0;

        var sanitizedCount = 0;
        foreach (var configFile in Directory.GetFiles(configDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configFile, ct);
                var root = JsonNode.Parse(json);
                if (root == null) continue;

                // 检查是否为插件配置文件格式（包含 groups 和 items 数组）
                var itemsNode = root["items"] as JsonArray;
                if (itemsNode == null || root["groups"] == null)
                    continue;

                var modified = false;
                foreach (var item in itemsNode)
                {
                    if (item == null) continue;

                    var itemType = item["type"]?.GetValue<string>() ?? "";
                    var contentNode = item["content"] as JsonObject;

                    // 根据字段类型决定默认值
                    string defaultValue;
                    if ((itemType == "select" || itemType == "radio") && contentNode != null && contentNode.Count > 0)
                    {
                        // select/radio 类型：使用 content 中第一个选项的 key 作为默认值
                        defaultValue = contentNode.First().Key;
                    }
                    else
                    {
                        // text/password/textarea 等类型：清空为空字符串
                        defaultValue = "";
                    }

                    // 将值重置为默认值
                    item["value"] = defaultValue;
                    modified = true;
                }

                if (modified)
                {
                    // 格式化后写回暂存区的文件
                    var formattedJson = JsonSerializer.Serialize(root, CreateReadableJsonOptions());
                    await File.WriteAllTextAsync(configFile, formattedJson, System.Text.Encoding.UTF8, ct);
                    sanitizedCount++;
                    _logger.LogDebug("已清洗配置文件: {File}", Path.GetFileName(configFile));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清洗配置文件失败: {File}", Path.GetFileName(configFile));
            }
        }

        return sanitizedCount;
    }

    /// <summary>
    /// 获取可打包的模块列表
    /// </summary>
    public List<ModuleInfo> GetPackageableModules()
    {
        var modules = new List<ModuleInfo>();

        var devDir = GetDevModuleSourceDir();
        if (devDir != null && Directory.Exists(devDir))
        {
            foreach (var moduleDir in Directory.GetDirectories(devDir))
            {
                var moduleJsonPath = FindFile(moduleDir, "module.json");
                if (moduleJsonPath != null)
                {
                    try
                    {
                        var json = File.ReadAllText(moduleJsonPath);
                        var manifest = JsonSerializer.Deserialize<ModuleManifest>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (manifest != null)
                        {
                            // 探测是否为源码版：server 目录下存在 .csproj 即视为有源码可打包
                            var serverDir = Path.Combine(moduleDir, "server");
                            var hasServer = Directory.Exists(serverDir);
                            var isSourcePackage = hasServer &&
                                Directory.GetFiles(serverDir, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;

                            modules.Add(new ModuleInfo
                            {
                                Id = manifest.Id,
                                Name = manifest.Name ?? manifest.Id,
                                Version = manifest.Version ?? "1.0.0",
                                Path = moduleDir,
                                HasServer = hasServer,
                                HasWeb = Directory.Exists(Path.Combine(moduleDir, "web")),
                                HasClient = Directory.Exists(Path.Combine(moduleDir, "client")),
                                IsSourcePackage = isSourcePackage
                            });
                        }
                    }
                    catch { }
                }
            }
        }

        return modules;
    }
}

/// <summary>
/// 模块信息
/// </summary>
public sealed class ModuleInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("moduleId")]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool HasServer { get; set; }
    public bool HasWeb { get; set; }
    public bool HasClient { get; set; }

    /// <summary>
    /// 是否为源码版模块（server 目录下存在 .csproj）。
    /// false 表示该模块是从编译包安装的 DLL 版，没有源码可供打源码包。
    /// </summary>
    public bool IsSourcePackage { get; set; }
}
