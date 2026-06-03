using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Ginkgo.Api.Auth;
using Ginkgo.Domain;
using Ginkgo.Domain.Modules;
using Microsoft.Extensions.Hosting;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块上传验证结果
/// </summary>
public sealed class ModuleUploadValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public ModuleManifest? Manifest { get; set; }
    public InstallSpec? InstallSpec { get; set; }
    public string? ExtractedPath { get; set; }

    /// <summary>
    /// 一次性 upload token，用于 confirm-install 阶段反查解压目录（P0-4 防 ExtractedPath 伪造）。
    /// 由 ModuleUploadService.RegisterUploadToken 在校验成功后填充；客户端只持有该 token，
    /// 下一步直接 POST /confirm-install { uploadId }。
    /// </summary>
    public string? UploadId { get; set; }

    /// <summary>
    /// 是否为源码包（包含 .csproj 文件）。false 表示编译 DLL 包。
    /// </summary>
    public bool IsSourcePackage { get; set; }

    /// <summary>
    /// module.json 文件的原始字节内容（用于签名验证）
    /// </summary>
    public byte[]? ModuleJsonRawBytes { get; set; }

    /// <summary>
    /// 文件哈希校验结果
    /// </summary>
    public ModuleHashValidationResult? HashValidation { get; set; }

    /// <summary>
    /// 签名验证结果
    /// </summary>
    public ModuleSignatureValidationResult? SignatureValidation { get; set; }

    /// <summary>
    /// 安全警告信息汇总
    /// </summary>
    public List<string> SecurityWarnings { get; set; } = new();

    /// <summary>
    /// 阶段 C 新增：随插件包一同安装的 license.lic 文件字节（来自插件商城签发）。
    /// 由远程安装链路（PluginStoreController.InstallPlugin）注入；本地手动上传场景为 null。
    /// </summary>
    public byte[]? LicenseFileBytes { get; set; }

    /// <summary>license 文件名（默认 license.lic，可由商城返回时覆盖）</summary>
    public string LicenseFileName { get; set; } = "license.lic";

    /// <summary>license 验签结果（由 PluginStoreController 调用 LicenseFileVerifier 后填充）</summary>
    public LicenseFileValidationResult? LicenseValidation { get; set; }
}

/// <summary>
/// 模块安装结果（带回滚支持）
/// </summary>
public sealed class ModuleInstallResultEx
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ModuleId { get; set; }
    public string? Version { get; set; }
    public List<string> ExecutedSteps { get; set; } = new();
    public List<string> RollbackSteps { get; set; } = new();
}

/// <summary>
/// 安装规范（从install.json读取）
/// </summary>
public sealed class InstallSpec
{
    public string? ModuleId { get; set; }
    public string? Description { get; set; }
    public string[]? SqlScripts { get; set; }
    public string[]? UninstallSql { get; set; }
    public Dictionary<string, object>? Config { get; set; }
    public MenusSpec? Menus { get; set; }
    public WebAssetsSpec? WebAssets { get; set; }
    public string? SupportedClients { get; set; }
}

/// <summary>
/// Web资源配置
/// </summary>
public sealed class WebAssetsSpec
{
    public string? SourcePath { get; set; }
    public string? TargetPath { get; set; }
}

/// <summary>
/// 菜单配置
/// </summary>
public sealed class MenusSpec
{
    public string? RootCode { get; set; }
    public List<MenuItemSpec>? Items { get; set; }
}

/// <summary>
/// 菜单项配置
/// </summary>
public sealed class MenuItemSpec
{
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Type { get; set; } = "Menu";
    public string? ItemMode { get; set; }
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public string? Code { get; set; }
    public string? Resource { get; set; }
    public string? Method { get; set; }
    public string? ParentCode { get; set; }
    public string? WebRouteUrl { get; set; }
    public string? WebDisplayMode { get; set; }
    public string? SupportedClients { get; set; }
    public int SortOrder { get; set; }
    public bool Hidden { get; set; }
}

/// <summary>
/// 模块上传和安装服务
/// </summary>
public sealed class ModuleUploadService
{
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _env;
    private readonly ModuleSqlExecutor _sqlExecutor;
    private readonly InstalledModulesStore _store;
    private readonly IConfiguration _config;
    private readonly SolutionManager? _solutionManager;
    private readonly WebModuleManager? _webModuleManager;
    private readonly ServerModuleManager? _serverModuleManager;
    private readonly PermissionCacheInvalidator? _permissionCacheInvalidator;
    private readonly ModuleHashValidator _hashValidator;
    private readonly ModuleSignatureVerifier _signatureVerifier;

    /// <summary>
    /// 上传 token 注册表：uploadId → 解压目录绝对路径与过期时间。
    /// 设计目标（P0-4）：上传成功后服务端只把 uploadId 返回前端，前端 confirm-install 时用 uploadId 反查，
    /// 避免客户端能控制 ExtractedPath 字段以欺骗服务端跳过重校验。
    /// </summary>
    private static readonly ConcurrentDictionary<string, UploadTokenEntry> _uploadTokens = new();

    /// <summary>token 的有效期，30 分钟内必须 confirm，否则被 cleanup 服务清理。</summary>
    private static readonly TimeSpan UploadTokenLifetime = TimeSpan.FromMinutes(30);

    private sealed record UploadTokenEntry(string ExtractedPath, DateTimeOffset ExpiresAt);

    public ModuleUploadService(
        IServiceProvider services,
        IHostEnvironment env,
        ModuleSqlExecutor sqlExecutor,
        InstalledModulesStore store,
        IConfiguration config,
        ModuleHashValidator hashValidator,
        ModuleSignatureVerifier signatureVerifier,
        SolutionManager? solutionManager = null,
        WebModuleManager? webModuleManager = null,
        ServerModuleManager? serverModuleManager = null,
        PermissionCacheInvalidator? permissionCacheInvalidator = null)
    {
        _services = services;
        _env = env;
        _sqlExecutor = sqlExecutor;
        _store = store;
        _config = config;
        _hashValidator = hashValidator;
        _signatureVerifier = signatureVerifier;
        _solutionManager = solutionManager;
        _webModuleManager = webModuleManager;
        _serverModuleManager = serverModuleManager;
        _permissionCacheInvalidator = permissionCacheInvalidator;
    }

    /// <summary>
    /// 获取临时上传目录（仅本类内部使用）。
    /// </summary>
    private string GetTempUploadDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ginkgo_module_uploads");
        if (!Directory.Exists(tempDir))
            Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>
    /// 公开访问器：返回临时上传目录的绝对路径，给 confirm-install 校验前缀使用。
    /// </summary>
    public string GetTempUploadDirPublic() => Path.GetFullPath(GetTempUploadDir());

    /// <summary>
    /// 校验某个绝对路径是否落在 GetTempUploadDir() 之下。confirm-install 时用作路径前缀防御。
    /// </summary>
    public bool IsPathUnderTempUploadDir(string? absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return false;
        try
        {
            var full = Path.GetFullPath(absolutePath);
            var baseDir = GetTempUploadDirPublic();
            // 末尾补分隔符避免 ".../foo" 被误判为前缀 ".../fo"
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar))
                baseDir += Path.DirectorySeparatorChar;
            return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 注册一次性 uploadId，关联到指定的解压目录。
    /// </summary>
    public string RegisterUploadToken(string extractedPath)
    {
        var id = Guid.NewGuid().ToString("N");
        _uploadTokens[id] = new UploadTokenEntry(Path.GetFullPath(extractedPath), DateTimeOffset.UtcNow.Add(UploadTokenLifetime));
        return id;
    }

    /// <summary>
    /// 一次性消费 uploadId：命中且未过期返回路径并移除该 token；不命中返回 null。
    /// </summary>
    public string? ConsumeUploadToken(string? uploadId)
    {
        if (string.IsNullOrEmpty(uploadId)) return null;
        if (!_uploadTokens.TryRemove(uploadId, out var entry)) return null;
        if (entry.ExpiresAt < DateTimeOffset.UtcNow) return null;
        return entry.ExtractedPath;
    }

    /// <summary>
    /// 通过解压目录路径反查 uploadId（兼容老前端只传 ExtractedPath 的场景）。
    /// </summary>
    public string? FindUploadTokenByPath(string? extractedPath)
    {
        if (string.IsNullOrEmpty(extractedPath)) return null;
        var fullPath = Path.GetFullPath(extractedPath);
        foreach (var (id, entry) in _uploadTokens)
        {
            if (string.Equals(entry.ExtractedPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return id;
        }
        return null;
    }

    /// <summary>
    /// 重新对已解压的目录做完整校验（路径遍历、哈希、签名、版本兼容、SQL 脚本存在性）。
    /// 仅用于 confirm-install 流程：客户端只能传 uploadId，服务端用本方法重新跑一遍校验，
    /// 不再相信客户端"已校验"的状态字段（P0-4 安全修复）。
    /// </summary>
    public async Task<ModuleUploadValidationResult> RevalidateAsync(string extractedPath, CancellationToken ct = default)
    {
        var result = new ModuleUploadValidationResult();
        if (!Directory.Exists(extractedPath))
        {
            result.ErrorMessage = "解压目录不存在或已被清理，请重新上传";
            return result;
        }

        try
        {
            var moduleJsonPath = FindFile(extractedPath, "module.json");
            if (moduleJsonPath == null)
            {
                result.ErrorMessage = "模块包中未找到 module.json 文件";
                return result;
            }

            var moduleJson = await File.ReadAllTextAsync(moduleJsonPath, ct);
            var manifest = JsonSerializer.Deserialize<ModuleManifest>(moduleJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                result.ErrorMessage = "module.json 格式无效或缺少必要字段 (id)";
                return result;
            }
            if (!ModuleIdentifierValidator.IsSafeModuleId(manifest.Id))
            {
                result.ErrorMessage = $"module.json 中 id 不合法: {manifest.Id}";
                return result;
            }
            if (!string.IsNullOrEmpty(manifest.Version) && !ModuleIdentifierValidator.IsSafeVersion(manifest.Version))
            {
                result.ErrorMessage = $"module.json 中 version 不合法: {manifest.Version}";
                return result;
            }

            var moduleJsonRawBytes = System.Text.Encoding.UTF8.GetBytes(moduleJson);
            result.ModuleJsonRawBytes = moduleJsonRawBytes;

            var pathTraversalError = ValidateExtractedPaths(extractedPath);
            if (pathTraversalError != null)
            {
                result.ErrorMessage = pathTraversalError;
                return result;
            }

            var hashResult = await _hashValidator.ValidateAsync(manifest, extractedPath, ct);
            result.HashValidation = hashResult;
            result.SecurityWarnings.AddRange(hashResult.Warnings);
            if (!hashResult.IsValid)
            {
                result.ErrorMessage = $"文件哈希校验失败: {string.Join("; ", hashResult.Mismatches)}";
                return result;
            }

            var sigResult = _signatureVerifier.Verify(manifest, moduleJsonRawBytes);
            result.SignatureValidation = sigResult;
            result.SecurityWarnings.AddRange(sigResult.Warnings);
            if (!sigResult.IsValid)
            {
                result.ErrorMessage = sigResult.ErrorMessage;
                return result;
            }

            var (publisherOk, publisherMsg) = _signatureVerifier.ValidatePublisher(manifest.Publisher);
            if (!publisherOk)
            {
                result.ErrorMessage = publisherMsg;
                return result;
            }

            var versionError = ValidateVersionCompatibility(manifest);
            if (versionError != null)
            {
                result.ErrorMessage = versionError;
                return result;
            }

            // install.json
            var installJsonPath = FindFile(extractedPath, "install.json");
            InstallSpec? installSpec = null;
            if (installJsonPath != null)
            {
                var installJson = await File.ReadAllTextAsync(installJsonPath, ct);
                installSpec = JsonSerializer.Deserialize<InstallSpec>(installJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (installSpec?.SqlScripts != null)
                {
                    var baseDir = Path.GetDirectoryName(installJsonPath) ?? extractedPath;
                    foreach (var script in installSpec.SqlScripts)
                    {
                        var scriptPath = Path.Combine(baseDir, script);
                        if (!File.Exists(scriptPath))
                        {
                            result.ErrorMessage = $"SQL脚本文件不存在: {script}";
                            return result;
                        }
                    }
                }
            }

            // 检测包类型
            var serverDir = Path.Combine(extractedPath, "server");
            if (!Directory.Exists(serverDir))
            {
                foreach (var sub in Directory.GetDirectories(extractedPath))
                {
                    var candidate = Path.Combine(sub, "server");
                    if (Directory.Exists(candidate)) { serverDir = candidate; break; }
                }
            }
            result.IsSourcePackage = Directory.Exists(serverDir) &&
                Directory.GetFiles(serverDir, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;

            result.IsValid = true;
            result.Manifest = manifest;
            result.InstallSpec = installSpec;
            result.ExtractedPath = extractedPath;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"重校验模块包失败: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 获取模块仓库目录
    /// </summary>
    private string GetModulesRepoDir()
    {
        return Path.Combine(AppContext.BaseDirectory, "modules_repo");
    }

    /// <summary>
    /// 获取开发环境模块源码目录
    /// </summary>
    private string? GetDevModuleSourceDir()
    {
        if (!IsDevelopmentEnvironment())
            return null;

        // 尝试从配置获取
        var modulePath = _config.GetValue<string>("cudr.modulepath");
        if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
            return modulePath;

        // 向上查找仓库根目录的 src/Module
        var searchBases = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var baseDir in searchBases)
        {
            var current = new DirectoryInfo(baseDir);
            for (int i = 0; i < 8 && current != null; i++)
            {
                var candidate = Path.Combine(current.FullName, "src", "Module");
                if (Directory.Exists(candidate))
                {
                    // 仓库根目录特征：同级有 GinkgoAdmin.sln，或 src/Module 内含 Directory.Build.props
                    var slnExists = File.Exists(Path.Combine(current.FullName, "GinkgoAdmin.sln"));
                    var buildPropsExists = File.Exists(Path.Combine(candidate, "Directory.Build.props"));
                    if (slnExists || buildPropsExists)
                    {
                        return Path.GetFullPath(candidate);
                    }

                    // 兼容旧判定：存在任一 Ginkgo.Module.* 子目录也视为有效
                    var subDirs = Directory.GetDirectories(candidate);
                    if (subDirs.Any(d => Path.GetFileName(d).StartsWith("Ginkgo.Module.", StringComparison.OrdinalIgnoreCase)))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                current = current.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取Web源码目录（开发环境）
    /// </summary>
    /// <summary>
    /// 判断是否为开发环境
    /// </summary>
    private bool IsDevelopmentEnvironment()
    {
        return string.Equals(_env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 上传并验证模块包
    /// </summary>
    public async Task<ModuleUploadValidationResult> UploadAndValidateAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var result = new ModuleUploadValidationResult();

        // 验证文件扩展名
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".gmod.zip", StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorMessage = "仅支持 .zip 或 .gmod.zip 格式的模块包";
            return result;
        }

        // 创建临时目录
        var tempDir = GetTempUploadDir();
        var uploadId = Guid.NewGuid().ToString("N");
        var extractDir = Path.Combine(tempDir, uploadId);

        try
        {
            // 保存上传的文件
            var zipPath = Path.Combine(tempDir, $"{uploadId}.zip");
            using (var fs = new FileStream(zipPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(fs, ct);
            }

            // 解压文件（P1-3：使用 SafeZipExtractor 在解压前对 entry 做 ZipSlip / zip-bomb 校验，
            // 拒绝绝对路径、".." 段，并限制 entry 数量与累计解压字节）
            Directory.CreateDirectory(extractDir);
            SafeZipExtractor.ExtractToDirectory(zipPath, extractDir, overwrite: false);

            // 查找 module.json
            var moduleJsonPath = FindFile(extractDir, "module.json");
            if (moduleJsonPath == null)
            {
                result.ErrorMessage = "模块包中未找到 module.json 文件";
                return result;
            }

            // 解析 module.json
            var moduleJson = await File.ReadAllTextAsync(moduleJsonPath, ct);
            var manifest = JsonSerializer.Deserialize<ModuleManifest>(moduleJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                result.ErrorMessage = "module.json 格式无效或缺少必要字段 (id)";
                return result;
            }

            // ========== 供应链安全校验 ==========
            var moduleJsonRawBytes = System.Text.Encoding.UTF8.GetBytes(moduleJson);
            result.ModuleJsonRawBytes = moduleJsonRawBytes;

            // 1) 路径遍历防护：检查解压目录内所有文件是否在合法范围内
            var pathTraversalError = ValidateExtractedPaths(extractDir);
            if (pathTraversalError != null)
            {
                result.ErrorMessage = pathTraversalError;
                return result;
            }

            // 2) SHA256 文件级哈希校验
            var hashResult = await _hashValidator.ValidateAsync(manifest, extractDir, ct);
            result.HashValidation = hashResult;
            result.SecurityWarnings.AddRange(hashResult.Warnings);

            if (!hashResult.IsValid)
            {
                // 哈希校验失败时，强制模式直接拒绝；非强制模式也拒绝（哈希不匹配是硬错误）
                result.ErrorMessage = $"文件哈希校验失败: {string.Join("; ", hashResult.Mismatches)}";
                return result;
            }

            // P1-2：强制模式下要求 manifest 必须声明 files[] 哈希；
            // 旧包（无 files 字段）在生产环境直接拒绝，强制供应链建立哈希链。
            if (_signatureVerifier.Options.RequireFileHashes == true
                && (manifest.Files == null || manifest.Files.Length == 0))
            {
                result.ErrorMessage = "系统要求模块包声明 files[] 哈希，但 module.json 中未提供。请使用 gmod 工具重新打包。";
                return result;
            }

            // 3) 包级签名验证（ECDSA P-256）
            var sigResult = _signatureVerifier.Verify(manifest, moduleJsonRawBytes);
            result.SignatureValidation = sigResult;
            result.SecurityWarnings.AddRange(sigResult.Warnings);

            if (!sigResult.IsValid)
            {
                result.ErrorMessage = sigResult.ErrorMessage;
                return result;
            }

            // 4) 发布者白名单校验
            var (publisherOk, publisherMsg) = _signatureVerifier.ValidatePublisher(manifest.Publisher);
            if (!publisherOk)
            {
                result.ErrorMessage = publisherMsg;
                return result;
            }

            // 5) 版本兼容性检查
            var versionError = ValidateVersionCompatibility(manifest);
            if (versionError != null)
            {
                result.ErrorMessage = versionError;
                return result;
            }
            // ========== 供应链安全校验结束 ==========

            // 查找 install.json
            var installJsonPath = FindFile(extractDir, "install.json");
            InstallSpec? installSpec = null;
            if (installJsonPath != null)
            {
                var installJson = await File.ReadAllTextAsync(installJsonPath, ct);
                installSpec = JsonSerializer.Deserialize<InstallSpec>(installJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            // 验证SQL脚本存在性
            if (installSpec?.SqlScripts != null)
            {
                var baseDir = Path.GetDirectoryName(installJsonPath) ?? extractDir;
                foreach (var script in installSpec.SqlScripts)
                {
                    var scriptPath = Path.Combine(baseDir, script);
                    if (!File.Exists(scriptPath))
                    {
                        result.ErrorMessage = $"SQL脚本文件不存在: {script}";
                        return result;
                    }
                }
            }

            // 删除临时zip文件
            try { File.Delete(zipPath); } catch { }

            // 校验 manifest.Id / manifest.Version 字符集合法性（P0-5）
            if (!ModuleIdentifierValidator.IsSafeModuleId(manifest.Id))
            {
                result.ErrorMessage = $"module.json 中 id 不合法（仅允许字母开头 + 字母数字./-/_ 长度 ≤128）: {manifest.Id}";
                return result;
            }
            if (!string.IsNullOrEmpty(manifest.Version) && !ModuleIdentifierValidator.IsSafeVersion(manifest.Version))
            {
                result.ErrorMessage = $"module.json 中 version 不合法（应为 semver 风格）: {manifest.Version}";
                return result;
            }

            result.IsValid = true;
            result.Manifest = manifest;
            result.InstallSpec = installSpec;
            result.ExtractedPath = extractDir;
            // 注册一次性 uploadId，供 confirm-install 反查（P0-4）
            result.UploadId = RegisterUploadToken(extractDir);

            // 检测包类型：server 目录下有 .csproj 文件则为源码包，否则为 DLL 包
            var serverDir = Path.Combine(extractDir, "server");
            if (!Directory.Exists(serverDir))
            {
                // 可能在子目录中（如 Ginkgo.Module.xxx/server）
                var subDirs = Directory.GetDirectories(extractDir);
                foreach (var sub in subDirs)
                {
                    var candidate = Path.Combine(sub, "server");
                    if (Directory.Exists(candidate)) { serverDir = candidate; break; }
                }
            }
            result.IsSourcePackage = Directory.Exists(serverDir) &&
                Directory.GetFiles(serverDir, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;

            return result;
        }
        catch (Exception ex)
        {
            // 清理临时文件
            try
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
            }
            catch { }

            result.ErrorMessage = $"解析模块包失败: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 在目录中递归查找文件
    /// </summary>
    private string? FindFile(string directory, string fileName)
    {
        // 先在根目录查找
        var rootFile = Path.Combine(directory, fileName);
        if (File.Exists(rootFile))
            return rootFile;

        // 在 server 子目录查找
        var serverFile = Path.Combine(directory, "server", fileName);
        if (File.Exists(serverFile))
            return serverFile;

        // 递归查找
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
    /// 安装模块（带回滚支持）
    /// </summary>
    public async Task<ModuleInstallResultEx> InstallModuleAsync(
        ModuleUploadValidationResult validation,
        CancellationToken ct = default)
    {
        var result = new ModuleInstallResultEx();

        if (!validation.IsValid || validation.Manifest == null || validation.ExtractedPath == null)
        {
            result.Message = validation.ErrorMessage ?? "验证结果无效";
            return result;
        }

        var manifest = validation.Manifest;
        var installSpec = validation.InstallSpec;
        var extractedPath = validation.ExtractedPath;
        var isDev = IsDevelopmentEnvironment();
        var isSource = validation.IsSourcePackage;

        // 兜底防护：生产环境不支持安装（无源码工作区、无前端目录、DLL热加载需重启）
        if (!isDev)
        {
            result.Message = "生产环境不支持在线安装插件。请在开发环境中安装后重新部署。";
            return result;
        }

        // 端能力探测：根据当前主框架源码是否含 WPF / UNIAPP 端，决定是否安装对应端内容。
        // 开源版主框架若缺少 WPF 客户端及 WPF UI 项目，强行安装插件 WPF 端会导致解决方案生成报错；
        // 缺少 UNIAPP 端时也不应把移动端文件复制到移动端目录。此规则对本地上传安装与应用商店下载安装一致生效。
        var installWpf = HasWpfFrameworkProjects();
        var installUniapp = HasUniappFramework();
        if (!installWpf)
            result.ExecutedSteps.Add("检测到主框架不含 WPF 客户端/UI 项目，本次将跳过插件 WPF 端安装");
        if (!installUniapp)
            result.ExecutedSteps.Add("检测到主框架不含 UNIAPP 移动端目录，本次将跳过插件移动端安装");

        // 回滚操作列表
        var rollbackActions = new List<Func<Task>>();

        try
        {
            // Step 1: 开发环境 - 复制文件到 src/Module 目录
            if (isDev)
            {
                var devModuleDir = GetDevModuleSourceDir();
                if (devModuleDir != null)
                {
                    var targetModuleDir = Path.Combine(devModuleDir, manifest.Id);
                    
                    // 处理现有目录
                    string? backupDir = null;
                    if (Directory.Exists(targetModuleDir))
                    {
                        backupDir = targetModuleDir + ".backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        try
                        {
                            // 尝试移动目录作为备份
                            Directory.Move(targetModuleDir, backupDir);
                        }
                        catch (IOException)
                        {
                            // 如果移动失败（目录被占用），尝试删除后重建
                            try
                            {
                                // 先尝试删除目录内容
                                foreach (var file in Directory.GetFiles(targetModuleDir, "*", SearchOption.AllDirectories))
                                {
                                    try { File.Delete(file); } catch { }
                                }
                                foreach (var dir in Directory.GetDirectories(targetModuleDir, "*", SearchOption.AllDirectories).Reverse())
                                {
                                    try { Directory.Delete(dir); } catch { }
                                }
                                Directory.Delete(targetModuleDir, true);
                            }
                            catch
                            {
                                // 如果仍然失败，直接覆盖安装（不备份）
                                backupDir = null;
                            }
                        }
                        
                        if (backupDir != null)
                        {
                            rollbackActions.Add(async () =>
                            {
                                if (Directory.Exists(targetModuleDir))
                                    Directory.Delete(targetModuleDir, true);
                                if (backupDir != null && Directory.Exists(backupDir))
                                    Directory.Move(backupDir, targetModuleDir);
                                await Task.CompletedTask;
                            });
                        }
                    }

                    if (isSource)
                    {
                        // 源码包：直接复制整个目录结构
                        if (Directory.Exists(targetModuleDir))
                            CopyDirectoryOverwrite(extractedPath, targetModuleDir);
                        else
                            CopyDirectory(extractedPath, targetModuleDir);
                        result.ExecutedSteps.Add($"[源码包] 复制模块源文件到 {targetModuleDir}");
                    }
                    else
                    {
                        // 编译包（DLL 包）安装规则：
                        //   1) ZIP 根目录下的裸 *.dll/*.pdb/*.xml（legacy 格式）→ 落位 server/bin/
                        //   2) ZIP 根目录下的其余文件（install-manifest.json / README.md / LICENSE 等）→ 落位模块根目录
                        //   3) ZIP 中的标准模块目录（server/contracts/web/uniapp/client/web-plugin/uniapp-plugin/wpf-plugin）
                        //      → 保持原名落在模块根目录下（保持 server/bin/... 等子结构完整）
                        //   4) 其它未知子目录（legacy 的 sql/ 等）→ 放到 server/ 下，避免污染模块根目录
                        Directory.CreateDirectory(targetModuleDir);
                        var serverBinDir = Path.Combine(targetModuleDir, "server", "bin");

                        var standardRootDirs = new HashSet<string>(
                            new[] { "server", "contracts", "web", "uniapp", "client", "web-plugin", "uniapp-plugin", "wpf-plugin" },
                            StringComparer.OrdinalIgnoreCase);

                        // 1 & 2: 处理根目录文件
                        foreach (var file in Directory.GetFiles(extractedPath))
                        {
                            var ext = Path.GetExtension(file).ToLowerInvariant();
                            if (ext is ".dll" or ".pdb" or ".xml")
                            {
                                if (!Directory.Exists(serverBinDir)) Directory.CreateDirectory(serverBinDir);
                                File.Copy(file, Path.Combine(serverBinDir, Path.GetFileName(file)), true);
                            }
                            else
                            {
                                // install-manifest.json、README.md、LICENSE、CHANGELOG.md 等 → 放在模块根目录
                                File.Copy(file, Path.Combine(targetModuleDir, Path.GetFileName(file)), true);
                            }
                        }

                        // 3 & 4: 处理子目录
                        foreach (var subDir in Directory.GetDirectories(extractedPath))
                        {
                            var dirName = Path.GetFileName(subDir);
                            if (standardRootDirs.Contains(dirName))
                            {
                                // 标准模块目录 → 落在模块根目录下，保持原子结构
                                CopyDirectory(subDir, Path.Combine(targetModuleDir, dirName));
                            }
                            else
                            {
                                // 非标准目录 → 合并到 server/ 下（兼容 legacy：裸 DLL + sql/ 的打包格式）
                                CopyDirectory(subDir, Path.Combine(targetModuleDir, "server", dirName));
                            }
                        }
                        result.ExecutedSteps.Add($"[编译包] 已解包到 {targetModuleDir}");
                    }

                    // 端能力裁剪：主框架缺少 WPF 端时移除模块自带的 client/ 与独立 wpf-plugin/ 目录，
                    // 避免后续被加入解决方案导致生成报错；缺少 UNIAPP 端时移除 uniapp/ 与 uniapp-plugin/ 目录。
                    if (!installWpf)
                    {
                        RemoveModuleEndDirectory(targetModuleDir, "client", result);
                        RemoveModuleEndDirectory(targetModuleDir, "wpf-plugin", result);
                    }
                    if (!installUniapp)
                    {
                        RemoveModuleEndDirectory(targetModuleDir, "uniapp", result);
                        RemoveModuleEndDirectory(targetModuleDir, "uniapp-plugin", result);
                    }

                    // 如果没有备份，添加删除回滚
                    if (backupDir == null)
                    {
                        rollbackActions.Add(async () =>
                        {
                            if (Directory.Exists(targetModuleDir))
                                Directory.Delete(targetModuleDir, true);
                            await Task.CompletedTask;
                        });
                    }
                }

                // Web 前端文件由 Step 7 的 WebModuleManager 安装到 web/src/plugins/installed/
            }

            // Step 2: 执行SQL脚本
            if (installSpec?.SqlScripts != null && installSpec.SqlScripts.Length > 0)
            {
                var installJsonDir = FindInstallJsonDirectory(extractedPath);
                var scripts = installSpec.SqlScripts.Select(p => Path.Combine(installJsonDir, p)).ToList();

                // 注意：必须在执行 install.sql 之前注册卸载 SQL 回滚。
                // 因为 install.sql 大量使用 CREATE TABLE（DDL），MySQL 中的 DDL 会触发隐式提交，
                // 把事务边界切碎，使得 ExecuteScriptsAsync 内部的 ROLLBACK 只能回滚最后一段未提交的批次，
                // 之前已被 DDL 隐式提交的建表与种子数据会残留在库里。
                // 只有提前注册卸载脚本作为回滚动作，外层 catch 才会在 SQL 中途失败时执行 uninstall.sql，
                // 把残留的表/字典/数据清理干净，避免下次安装因重复主键继续报错。
                if (installSpec.UninstallSql != null && installSpec.UninstallSql.Length > 0)
                {
                    var uninstallScripts = installSpec.UninstallSql.Select(p => Path.Combine(installJsonDir, p)).ToList();
                    rollbackActions.Add(async () =>
                    {
                        try
                        {
                            await _sqlExecutor.ExecuteScriptsAsync(uninstallScripts, CancellationToken.None);
                        }
                        catch { }
                    });
                }

                await _sqlExecutor.ExecuteScriptsAsync(scripts, ct);
                result.ExecutedSteps.Add($"执行SQL脚本: {string.Join(", ", installSpec.SqlScripts)}");
            }

            // Step 3: 注册菜单
            if (installSpec?.Menus != null)
            {
                var sqlSpec = new ModuleSqlExecutor.InstallSpec
                {
                    ModuleId = manifest.Id,
                    Menus = new ModuleSqlExecutor.MenusSpec
                    {
                        RootCode = installSpec.Menus.RootCode,
                        Items = installSpec.Menus.Items?.Select(i => new ModuleSqlExecutor.MenuItemSpec
                        {
                            Name = i.Name,
                            Route = i.Route,
                            Type = i.Type,
                            ItemMode = i.ItemMode,
                            Icon = i.Icon,
                            Url = i.Url,
                            Code = i.Code,
                            Resource = i.Resource,
                            Method = i.Method,
                            ParentCode = i.ParentCode,
                            WebRouteUrl = i.WebRouteUrl,
                            WebDisplayMode = i.WebDisplayMode,
                            SupportedClients = i.SupportedClients,
                            Hidden = i.Hidden,
                            SortOrder = i.SortOrder
                        }).ToList()
                    }
                };

                await _sqlExecutor.ApplyMenusAsync(sqlSpec, manifest.Name ?? manifest.Id, manifest.Id, ct);
                result.ExecutedSteps.Add("注册菜单");

                // 添加菜单回滚
                rollbackActions.Add(async () =>
                {
                    try
                    {
                        await _sqlExecutor.RemoveMenusAsync(sqlSpec, CancellationToken.None);
                    }
                    catch { }
                });
            }

            // Step 4: 保存到模块仓库（生产环境）
            if (!isDev)
            {
                var repoDir = GetModulesRepoDir();
                if (!Directory.Exists(repoDir))
                    Directory.CreateDirectory(repoDir);

                var targetZipPath = Path.Combine(repoDir, $"{manifest.Id}.gmod.zip");
                
                // 备份现有包
                string? backupZip = null;
                if (File.Exists(targetZipPath))
                {
                    backupZip = targetZipPath + ".backup";
                    File.Move(targetZipPath, backupZip);
                }

                // 创建新的模块包
                ZipFile.CreateFromDirectory(extractedPath, targetZipPath);
                result.ExecutedSteps.Add($"保存模块包到 {targetZipPath}");

                rollbackActions.Add(async () =>
                {
                    try
                    {
                        if (File.Exists(targetZipPath))
                            File.Delete(targetZipPath);
                        if (backupZip != null && File.Exists(backupZip))
                            File.Move(backupZip, targetZipPath);
                    }
                    catch { }
                    await Task.CompletedTask;
                });

                // 删除备份
                if (backupZip != null && File.Exists(backupZip))
                {
                    try { File.Delete(backupZip); } catch { }
                }
            }

            // Step 5: 更新已安装模块记录（含 MenuRootCode，供卸载时兜底移除菜单）
            await _store.AddOrUpdateAsync(new InstalledModule
            {
                Id = manifest.Id,
                Name = manifest.Name ?? manifest.Id,
                Version = manifest.Version ?? "1.0.0",
                HasClient = manifest.HasClient,
                Enabled = true,
                InstalledAtUtc = DateTime.Now,
                Publisher = manifest.Publisher,
                Homepage = manifest.Homepage,
                MenuRootCode = installSpec?.Menus?.RootCode
            });
            result.ExecutedSteps.Add("更新模块安装记录");

            // 清理临时文件
            try
            {
                if (Directory.Exists(extractedPath))
                    Directory.Delete(extractedPath, true);
            }
            catch { }

            result.Ok = true;
            result.Message = isSource ? "模块安装成功（源码包）" : "模块安装成功（DLL包）";
            result.ModuleId = manifest.Id;
            result.Version = manifest.Version;
            _permissionCacheInvalidator?.InvalidateAll();

            // 阶段 C：将商城签发的 license.lic 写入安装目录（供运行期/巡检使用）
            if (validation.LicenseFileBytes != null && validation.LicenseFileBytes.Length > 0)
            {
                try
                {
                    string? licenseTargetDir = null;
                    if (isDev)
                    {
                        var devModuleDir = GetDevModuleSourceDir();
                        if (devModuleDir != null)
                            licenseTargetDir = Path.Combine(devModuleDir, manifest.Id, "server");
                    }
                    if (licenseTargetDir == null) licenseTargetDir = extractedPath;

                    Directory.CreateDirectory(licenseTargetDir);
                    var licensePath = Path.Combine(licenseTargetDir, validation.LicenseFileName ?? "license.lic");
                    await File.WriteAllBytesAsync(licensePath, validation.LicenseFileBytes, ct);
                    result.ExecutedSteps.Add($"已写入授权文件 license.lic（{validation.LicenseFileBytes.Length} 字节）");
                }
                catch (Exception ex)
                {
                    result.ExecutedSteps.Add($"写入 license.lic 失败（不影响安装）: {ex.Message}");
                }
            }

            // Step 6: 开发环境 + 源码包 - 将模块项目添加到解决方案
            if (isDev && isSource && _solutionManager != null)
            {
                try
                {
                    var devModuleDir = GetDevModuleSourceDir();
                    if (devModuleDir != null)
                    {
                        var moduleDir = Path.Combine(devModuleDir, manifest.Id);
                        var serverCsproj = Directory.GetFiles(Path.Combine(moduleDir, "server"), "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        // 仅当主框架支持 WPF 端时才把客户端项目加入解决方案；
                        // 开源版无 WPF 客户端/UI 项目时，client 端已在 Step 1 被裁剪，这里也不再引用，避免解决方案生成报错。
                        var clientCsproj = (installWpf && Directory.Exists(Path.Combine(moduleDir, "client")))
                            ? Directory.GetFiles(Path.Combine(moduleDir, "client"), "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
                            : null;
                        var contractsCsproj = Directory.Exists(Path.Combine(moduleDir, "contracts"))
                            ? Directory.GetFiles(Path.Combine(moduleDir, "contracts"), "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
                            : null;

                        if (serverCsproj != null)
                        {
                            await _solutionManager.AddModuleToSolutionAsync(manifest.Id, serverCsproj, clientCsproj, contractsCsproj, ct);
                            result.ExecutedSteps.Add("已将模块项目添加到解决方案");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 解决方案修改失败不影响安装结果
                    result.ExecutedSteps.Add($"添加到解决方案失败（不影响安装）: {ex.Message}");
                }
            }

            // Step 7: 安装 Web 前端文件
            if (_webModuleManager != null)
            {
                try
                {
                    var moduleBaseDir = isDev ? Path.Combine(GetDevModuleSourceDir()!, manifest.Id) : extractedPath;
                    await _webModuleManager.InstallWebFilesAsync(manifest.Id, moduleBaseDir, ct);
                    result.ExecutedSteps.Add("已安装 Web 前端文件");
                }
                catch (Exception ex)
                {
                    result.ExecutedSteps.Add($"安装 Web 前端文件失败（不影响安装）: {ex.Message}");
                }
            }

            // Step 7.1: 安装 UniApp 前端文件（仅当主框架包含 UNIAPP 移动端目录时执行）
            if (isDev && installUniapp)
            {
                try
                {
                    var moduleBaseDir = Path.Combine(GetDevModuleSourceDir()!, manifest.Id);
                    await InstallUniappFilesAsync(manifest.Id, moduleBaseDir, extractedPath, ct);
                    result.ExecutedSteps.Add("已安装 UniApp 前端文件");
                }
                catch (Exception ex)
                {
                    result.ExecutedSteps.Add($"安装 UniApp 前端文件失败（不影响安装）: {ex.Message}");
                }
            }
            else if (isDev && !installUniapp)
            {
                result.ExecutedSteps.Add("跳过 UniApp 前端文件安装（主框架不含 UNIAPP 移动端目录）");
            }

            // Step 7.5: 开发环境 + 源码包 - 安装 Backend NuGet 依赖
            if (isDev && isSource && _serverModuleManager != null)
            {
                try
                {
                    var devModuleDir = GetDevModuleSourceDir();
                    if (devModuleDir != null)
                    {
                        var moduleDir = Path.Combine(devModuleDir, manifest.Id);
                        var moduleJsonPath = Path.Combine(moduleDir, "server", "module.json");
                        var hostCsprojPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Server", "Ginkgo.Api", "Ginkgo.Api.csproj"));
                        await _serverModuleManager.InstallNugetDependenciesAsync(moduleJsonPath, hostCsprojPath, ct);
                        result.ExecutedSteps.Add("已探测并附加 NuGet 后端依赖");
                    }
                }
                catch (Exception ex)
                {
                    result.ExecutedSteps.Add($"NuGet 包依赖加载失败: {ex.Message}");
                }
            }

            // Step 8: 安装 WPF 客户端插件文件（从 wpf-plugin/ 目录到 src/Client/）
            //   仅当主框架包含 WPF 客户端及 WPF UI 项目时执行；开源版无 WPF 端时整体跳过。
            if (isDev && !installWpf)
            {
                result.ExecutedSteps.Add("跳过 WPF 客户端插件安装（主框架不含 WPF 客户端/UI 项目）");
            }
            if (isDev && installWpf)
            {
                try
                {
                    // 查找已安装模块目录中的 wpf-plugin/ 目录
                    var moduleBaseDir = Path.Combine(GetDevModuleSourceDir()!, manifest.Id);
                    var wpfPluginDir = Path.Combine(moduleBaseDir, "wpf-plugin");

                    // 如果模块目录中没有 wpf-plugin/，尝试从解压目录中查找
                    if (!Directory.Exists(wpfPluginDir))
                        wpfPluginDir = Path.Combine(extractedPath, "wpf-plugin");

                    if (Directory.Exists(wpfPluginDir))
                    {
                        // 从 install-manifest.json 读取 WPF 插件目标路径
                        var manifestPath = FindFile(moduleBaseDir, "install-manifest.json")
                            ?? FindFile(extractedPath, "install-manifest.json");
                        string? wpfTargetRelPath = null;

                        if (manifestPath != null && File.Exists(manifestPath))
                        {
                            try
                            {
                                var manifestJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                    await File.ReadAllTextAsync(manifestPath, ct));
                                if (manifestJson.TryGetProperty("installPaths", out var paths) &&
                                    paths.TryGetProperty("wpf-plugin", out var wpfPath))
                                {
                                    wpfTargetRelPath = wpfPath.GetString();
                                }
                            }
                            catch { }
                        }

                        // 计算目标路径：优先使用 manifest 中的路径，否则默认使用模块目录名
                        // 仓库根目录：GetDevModuleSourceDir() 返回 .../src/Module，往上两级即为仓库根
                        var devModuleBase = GetDevModuleSourceDir();
                        var repoRoot = devModuleBase != null ? Path.GetFullPath(Path.Combine(devModuleBase, "..", "..")) : null;
                        if (repoRoot != null)
                        {
                            string targetDir;
                            if (!string.IsNullOrEmpty(wpfTargetRelPath))
                            {
                                targetDir = Path.Combine(repoRoot, wpfTargetRelPath.Replace('/', Path.DirectorySeparatorChar));
                            }
                            else
                            {
                                // 默认路径：src/Client/{wpf-plugin目录中第一个 .csproj 所在目录名}
                                var pluginDirName = Path.GetFileName(wpfPluginDir);
                                targetDir = Path.Combine(repoRoot, "src", "Client", pluginDirName);
                            }

                            if (Directory.Exists(targetDir))
                                CopyDirectoryOverwrite(wpfPluginDir, targetDir);
                            else
                                CopyDirectory(wpfPluginDir, targetDir);

                            result.ExecutedSteps.Add($"已安装 WPF 客户端插件到 {targetDir}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // WPF 客户端安装失败不影响整体安装结果
                    result.ExecutedSteps.Add($"安装 WPF 客户端插件失败（不影响安装）: {ex.Message}");
                }
            }

            // Step 9b: 开发环境 + 编译包 - 跳过 dotnet build，直接热加载（DLL 已随包一同安装）
            if (isDev && !isSource)
            {
                try
                {
                    var hot = _services.GetService(typeof(ModuleHotReloader)) as ModuleHotReloader;
                    if (hot != null)
                    {
                        var ok = await hot.EnableAsync(manifest.Id, ct);
                        result.ExecutedSteps.Add(ok
                            ? "已热加载编译包 DLL 并注册 MVC 路由"
                            : "编译包热加载失败（将在下次重启 API 后生效）");
                    }
                    else
                    {
                        result.ExecutedSteps.Add("ModuleHotReloader 未注册，跳过热加载（重启 API 后生效）");
                    }
                }
                catch (Exception hex)
                {
                    result.ExecutedSteps.Add($"编译包热加载失败（重启后生效）: {hex.Message}");
                }
            }

            // Step 9: 开发环境 + 源码包 - 编译模块并热加载到运行时
            if (isDev && isSource)
            {
                try
                {
                    var devModuleDir = GetDevModuleSourceDir();
                    if (devModuleDir != null)
                    {
                        var moduleDir = Path.Combine(devModuleDir, manifest.Id);
                        var serverDir = Path.Combine(moduleDir, "server");
                        string? csproj = null;
                        if (Directory.Exists(serverDir))
                        {
                            csproj = Directory.GetFiles(serverDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        }

                        if (csproj != null)
                        {
                            var build = _services.GetService(typeof(ModuleDotnetBuildService)) as ModuleDotnetBuildService;
                            if (build != null)
                            {
                                var buildResult = await build.BuildAsync(csproj, ct);
                                if (buildResult.Ok)
                                {
                                    result.ExecutedSteps.Add($"已编译模块 ({buildResult.ElapsedMs} ms)");

                                    // 热加载：直接解析 ModuleHotReloader（同一程序集内的公开类型）
                                    try
                                    {
                                        var hot = _services.GetService(typeof(ModuleHotReloader)) as ModuleHotReloader;
                                        if (hot != null)
                                        {
                                            var ok = await hot.EnableAsync(manifest.Id, ct);
                                            result.ExecutedSteps.Add(ok
                                                ? "已热加载模块 DLL 并注册 MVC 路由"
                                                : "热加载失败（将在下次重启 API 后生效）");
                                        }
                                        else
                                        {
                                            result.ExecutedSteps.Add("ModuleHotReloader 未注册，跳过热加载（重启 API 后生效）");
                                        }
                                    }
                                    catch (Exception hex)
                                    {
                                        result.ExecutedSteps.Add($"热加载失败（重启后生效）: {hex.Message}");
                                    }
                                }
                                else
                                {
                                    // 编译失败不回滚（源码已写入），让用户看到错误再自行处理
                                    var err = string.IsNullOrWhiteSpace(buildResult.StdErr) ? buildResult.StdOut : buildResult.StdErr;
                                    var snippet = (err ?? string.Empty);
                                    if (snippet.Length > 400) snippet = snippet.Substring(snippet.Length - 400);
                                    result.ExecutedSteps.Add($"编译失败（请检查源码并手动构建）: {buildResult.Message} {snippet}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.ExecutedSteps.Add($"构建/热加载阶段异常（不影响安装）: {ex.Message}");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            // 执行回滚
            result.Message = $"安装失败: {ex.Message}，正在回滚...";
            
            foreach (var rollback in rollbackActions.AsEnumerable().Reverse())
            {
                try
                {
                    await rollback();
                    result.RollbackSteps.Add("回滚操作执行成功");
                }
                catch (Exception rollbackEx)
                {
                    result.RollbackSteps.Add($"回滚操作失败: {rollbackEx.Message}");
                }
            }

            // 清理临时文件
            try
            {
                if (Directory.Exists(extractedPath))
                    Directory.Delete(extractedPath, true);
            }
            catch { }

            result.Message = $"安装失败: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 查找 install.json 所在目录
    /// </summary>
    private string FindInstallJsonDirectory(string extractedPath)
    {
        var installJsonPath = FindFile(extractedPath, "install.json");
        if (installJsonPath != null)
            return Path.GetDirectoryName(installJsonPath) ?? extractedPath;
        return extractedPath;
    }

    /// <summary>
    /// 安装 UniApp 前端文件
    /// 打包时 UniApp 文件存在于两个可能的位置：
    /// 1. 模块自带的 uniapp/ 目录 → 安装到 src/Module/{id}/uniapp/（Step1 已处理）
    /// 2. 独立的 uniapp-plugin/ 目录 → 安装到 uniapp/pgzx/pages/plugins/{shortName}/
    /// </summary>
    private async Task InstallUniappFilesAsync(string moduleId, string moduleBaseDir, string extractedPath, CancellationToken ct)
    {
        // 查找 uniapp-plugin/ 目录（打包时步骤6放入的独立 UniApp 插件）
        var uniappPluginDir = Path.Combine(moduleBaseDir, "uniapp-plugin");
        if (!Directory.Exists(uniappPluginDir))
            uniappPluginDir = Path.Combine(extractedPath, "uniapp-plugin");

        if (!Directory.Exists(uniappPluginDir))
            return; // 该模块没有独立的 UniApp 插件

        // 计算目标路径
        var repoRoot = GetRepoRoot();
        if (repoRoot == null) return;

        // 从 install-manifest.json 获取目标路径
        string? targetRelPath = null;
        var manifestPath = FindFile(moduleBaseDir, "install-manifest.json")
            ?? FindFile(extractedPath, "install-manifest.json");

        if (manifestPath != null && File.Exists(manifestPath))
        {
            try
            {
                var manifestJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                    await File.ReadAllTextAsync(manifestPath, ct));
                if (manifestJson.TryGetProperty("installPaths", out var paths) &&
                    paths.TryGetProperty("uniapp-plugin", out var uniPath))
                {
                    targetRelPath = uniPath.GetString();
                }
            }
            catch { }
        }

        // 默认路径：uniapp/pgzx/pages/plugins/{shortName}/
        if (string.IsNullOrEmpty(targetRelPath))
        {
            var shortName = moduleId.Replace("Ginkgo.Module.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
            targetRelPath = $"uniapp/pgzx/pages/plugins/{shortName}/";
        }

        var targetDir = Path.Combine(repoRoot, targetRelPath.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(targetDir))
            CopyDirectoryOverwrite(uniappPluginDir, targetDir);
        else
            CopyDirectory(uniappPluginDir, targetDir);
    }

    /// <summary>
    /// 获取仓库根目录
    /// </summary>
    private string? GetRepoRoot()
    {
        var devModuleBase = GetDevModuleSourceDir();
        return devModuleBase != null ? Path.GetFullPath(Path.Combine(devModuleBase, "..", "..")) : null;
    }

    /// <summary>
    /// 检测当前主框架源码是否包含 WPF 客户端与 WPF UI 项目。
    /// 开源版主框架不含 WPF 端（src/Client 下没有 Ginkgo.Wpf / Ginkgo.UI 项目），
    /// 此时插件的 WPF 端（模块自带 client/ 源码、独立 wpf-plugin/）不应安装，
    /// 否则会把缺失依赖的 WPF 客户端项目加入解决方案，导致解决方案生成/编译报错。
    /// </summary>
    private bool HasWpfFrameworkProjects()
    {
        var repoRoot = GetRepoRoot();
        if (repoRoot == null) return false;

        var clientBase = Path.Combine(repoRoot, "src", "Client");
        if (!Directory.Exists(clientBase)) return false;

        // 主框架 WPF 宿主项目与公共 UI 库均存在，才认定该框架支持 WPF 端
        var wpfCsproj = Path.Combine(clientBase, "Ginkgo.Wpf", "Ginkgo.Wpf.csproj");
        var uiCsproj = Path.Combine(clientBase, "Ginkgo.UI", "Ginkgo.UI.csproj");
        return File.Exists(wpfCsproj) && File.Exists(uiCsproj);
    }

    /// <summary>
    /// 检测当前主框架源码是否包含 UNIAPP 移动端工程目录。
    /// 开源版/精简版主框架可能不含 uniapp 端，此时插件的移动端文件不应复制到移动端目录，
    /// 仅安装 API 与 WEB 端即可。
    /// </summary>
    private bool HasUniappFramework()
    {
        var repoRoot = GetRepoRoot();
        if (repoRoot == null) return false;

        var uniappRoot = Path.Combine(repoRoot, "uniapp");
        return Directory.Exists(uniappRoot);
    }

    /// <summary>
    /// 删除已复制到模块源码目录下的某个端目录（如 client / wpf-plugin / uniapp / uniapp-plugin），
    /// 用于主框架缺少对应端时清理掉不需要安装的内容。删除失败仅记录步骤，不影响安装。
    /// </summary>
    private static void RemoveModuleEndDirectory(string moduleDir, string endDirName, ModuleInstallResultEx result)
    {
        try
        {
            var target = Path.Combine(moduleDir, endDirName);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
                result.ExecutedSteps.Add($"主框架缺少对应端，已跳过并移除 {endDirName}/ 目录");
            }
        }
        catch (Exception ex)
        {
            result.ExecutedSteps.Add($"清理 {endDirName}/ 目录失败（不影响安装）: {ex.Message}");
        }
    }

    /// <summary>
    /// 复制目录
    /// </summary>
    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, targetSubDir);
        }
    }

    /// <summary>
    /// 复制目录（覆盖模式，目标目录已存在时逐个覆盖文件）
    /// </summary>
    private void CopyDirectoryOverwrite(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            try
            {
                File.Copy(file, targetFile, true);
            }
            catch (IOException)
            {
                // 如果文件被占用，尝试先删除再复制
                try
                {
                    File.Delete(targetFile);
                    File.Copy(file, targetFile);
                }
                catch { /* 忽略无法复制的文件 */ }
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectoryOverwrite(dir, targetSubDir);
        }
    }

    /// <summary>
    /// 路径遍历防护：校验解压后的所有文件是否都在解压目录范围内
    /// 并检查文件扩展名是否在白名单中。
    /// 【兼容跨平台 native】凡是落在 runtimes/&lt;rid&gt;/native/ 或 runtimes/&lt;rid&gt;/nativeassets/... 子路径下的文件，
    /// 属于 dotnet publish 的标准跨平台原生产物（SQLite、ONNX、ICU 等），不参与扩展名白名单校验——
    /// 这些目录由 SDK 统一管控，扩展名随平台各异（.so/.dylib/.a/.icu/.dat 等），逐个列举无意义。
    /// </summary>
    private static string? ValidateExtractedPaths(string extractDir)
    {
        var normalizedBase = Path.GetFullPath(extractDir) + Path.DirectorySeparatorChar;

        // 允许的文件扩展名白名单（针对 runtimes/ 之外的模块资源）
        // .so / .dylib / .a 是常见的原生库扩展名，即便不在 runtimes/ 下（例如少数项目把 native 直接平铺到 bin/）也放行，
        // 其信任级别与 .dll 相同（二进制可执行文件），仅在匹配当前 RID 时才会被 P/Invoke 加载。
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".pdb", ".xml", ".sql", ".json", ".sample",
            ".so", ".dylib", ".a",
            ".vue", ".ts", ".tsx", ".js", ".jsx", ".css", ".scss", ".less", ".html", ".htm",
            ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico",
            ".md", ".txt", ".config", ".yaml", ".yml",
            ".xaml", ".cs", ".csproj", ".sln", ".props", ".targets",
            ".map", ".woff", ".woff2", ".ttf", ".eot"
        };

        string[] allFiles;
        try
        {
            allFiles = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            return $"无法扫描解压目录: {ex.Message}";
        }

        foreach (var file in allFiles)
        {
            var fullPath = Path.GetFullPath(file);

            // 检查路径是否逃逸出解压目录
            if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                return $"检测到路径遍历攻击: 文件 {Path.GetFileName(file)} 逃逸出模块包范围";
            }

            var ext = Path.GetExtension(file);

            // 无扩展名文件（LICENSE / Dockerfile 等）直接放行
            if (string.IsNullOrEmpty(ext))
                continue;

            // 白名单直接放行
            if (allowedExtensions.Contains(ext))
                continue;

            // 【路径豁免】dotnet publish 的跨平台 native 产物目录：
            //   runtimes/<rid>/native/**          （大多数场景：.so / .dylib / .dll）
            //   runtimes/<rid>/nativeassets/<tfm>/** （WASM/iOS/tvOS 等：.a 静态库、.icu、.dat 等）
            // 由 SDK 统一管控，扩展名因 RID 差异极大，整体放行避免打地鼠式维护
            var relPath = Path.GetRelativePath(extractDir, file);
            if (IsUnderNativeRuntimesDir(relPath))
                continue;

            return $"模块包包含不允许的文件类型: {relPath} (扩展名: {ext})";
        }

        return null;
    }

    /// <summary>
    /// 判断相对路径是否落在 .NET 标准的跨平台原生产物目录下：
    /// runtimes/&lt;rid&gt;/native/ 或 runtimes/&lt;rid&gt;/nativeassets/&lt;anything&gt;/...。
    /// 这些目录由 dotnet publish 根据 NuGet 包的 runtimes/ 自动拷入，扩展名随平台各异
    /// （.so/.dylib/.a/.icu/.dat 等），整体豁免扩展名白名单。
    /// </summary>
    private static bool IsUnderNativeRuntimesDir(string relPath)
    {
        if (string.IsNullOrEmpty(relPath)) return false;
        // 统一分隔符，并在头部补一个 '/'，使用 "/runtimes/" 作为唯一定位锚点
        var p = "/" + relPath.Replace('\\', '/').Trim('/');
        int idx = p.IndexOf("/runtimes/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        var tail = p.Substring(idx + "/runtimes/".Length); // "<rid>/native(assets)/..."
        var parts = tail.Split('/');
        if (parts.Length < 3) return false;
        // parts[0] = rid，例如 linux-x64 / osx-arm64 / browser-wasm / ios-arm64
        // parts[1] = "native" 或 "nativeassets"
        var segment = parts[1];
        return string.Equals(segment, "native", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "nativeassets", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 版本兼容性检查：校验模块要求的最低框架版本
    /// </summary>
    private static string? ValidateVersionCompatibility(ModuleManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.MinAppVersion))
            return null;

        // 获取当前框架版本
        var appVersion = typeof(ModuleUploadService).Assembly.GetName().Version;
        if (appVersion == null)
            return null;

        if (Version.TryParse(manifest.MinAppVersion, out var requiredVersion))
        {
            // 比较主版本和次版本
            var currentComparable = new Version(appVersion.Major, appVersion.Minor, appVersion.Build >= 0 ? appVersion.Build : 0);
            if (currentComparable < requiredVersion)
            {
                return $"模块 {manifest.Id} 要求框架最低版本 {manifest.MinAppVersion}，" +
                       $"当前框架版本 {currentComparable}，请升级框架后再安装。";
            }
        }

        return null;
    }
}
