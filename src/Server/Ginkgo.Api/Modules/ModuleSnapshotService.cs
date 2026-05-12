using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 快照元数据
/// </summary>
public sealed class SnapshotMetadata
{
    public string ModuleId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string SnapshotType { get; set; } = "install"; // install / upgrade / manual
    public string? FilesArchivePath { get; set; }
    public string? MenusSnapshotPath { get; set; }
    public string? ConfigSnapshotPath { get; set; }
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// 模块快照服务：安装前自动创建快照，支持手动回滚。
/// 快照存储在 modules_repo/snapshots/{moduleId}/{timestamp}/ 目录下。
/// </summary>
public sealed class ModuleSnapshotService
{
    private readonly ILogger<ModuleSnapshotService> _logger;
    private readonly ModuleSqlExecutor _sqlExecutor;
    private readonly string _snapshotsBaseDir;

    /// <summary>
    /// 最多保留的快照数量
    /// </summary>
    private const int MaxSnapshots = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ModuleSnapshotService(ILogger<ModuleSnapshotService> logger, ModuleSqlExecutor sqlExecutor)
    {
        _logger = logger;
        _sqlExecutor = sqlExecutor;
        _snapshotsBaseDir = Path.Combine(AppContext.BaseDirectory, "modules_repo", "snapshots");
    }

    /// <summary>
    /// 安装前创建完整快照（文件 + 菜单 + 配置）
    /// </summary>
    /// <param name="moduleId">模块 ID</param>
    /// <param name="moduleDir">模块文件目录（若存在）</param>
    /// <param name="installSpec">安装规范（用于获取菜单根 Code）</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>快照元数据，null 表示模块不存在无需快照</returns>
    public async Task<SnapshotMetadata?> CreateSnapshotAsync(
        string moduleId,
        string? moduleDir,
        ModuleSqlExecutor.InstallSpec? installSpec,
        string? operatorId,
        CancellationToken ct)
    {
        // 如果模块目录不存在，说明是全新安装，无需快照
        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir))
        {
            _logger.LogInformation("模块 {ModuleId} 无现有目录，跳过快照", moduleId);
            return null;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var snapshotDir = Path.Combine(_snapshotsBaseDir, moduleId, timestamp);
        Directory.CreateDirectory(snapshotDir);

        var metadata = new SnapshotMetadata
        {
            ModuleId = moduleId,
            Version = timestamp,
            CreatedBy = operatorId,
            SnapshotType = "install"
        };

        try
        {
            // 1) 文件快照：将模块目录打包
            var archivePath = Path.Combine(snapshotDir, "files.zip");
            ZipFile.CreateFromDirectory(moduleDir, archivePath);
            metadata.FilesArchivePath = archivePath;
            metadata.FileSizeBytes = new FileInfo(archivePath).Length;
            _logger.LogInformation("模块 {ModuleId} 文件快照已创建: {Size} bytes", moduleId, metadata.FileSizeBytes);

            // 2) 菜单快照：导出当前菜单树 JSON
            if (installSpec?.Menus?.RootCode != null)
            {
                try
                {
                    var menuSpec = await _sqlExecutor.ExportMenuTreeAsync(installSpec.Menus.RootCode, ct);
                    if (menuSpec != null)
                    {
                        var menusPath = Path.Combine(snapshotDir, "menus.json");
                        var menusJson = JsonSerializer.Serialize(menuSpec, JsonOptions);
                        await File.WriteAllTextAsync(menusPath, menusJson, ct);
                        metadata.MenusSnapshotPath = menusPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "模块 {ModuleId} 菜单快照失败", moduleId);
                }
            }

            // 3) 配置快照：备份模块配置文件
            var configDir = Path.Combine(moduleDir, "server", "config");
            if (Directory.Exists(configDir))
            {
                var configSnapshotDir = Path.Combine(snapshotDir, "config");
                CopyDirectory(configDir, configSnapshotDir);
                metadata.ConfigSnapshotPath = configSnapshotDir;
            }

            // 保存快照元数据
            var metadataPath = Path.Combine(snapshotDir, "metadata.json");
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions), ct);

            // 清理旧快照
            await CleanupOldSnapshotsAsync(moduleId, ct);

            _logger.LogInformation("模块 {ModuleId} 完整快照创建成功: {Dir}", moduleId, snapshotDir);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模块 {ModuleId} 快照创建失败", moduleId);
            // 清理失败的快照
            try { if (Directory.Exists(snapshotDir)) Directory.Delete(snapshotDir, true); } catch { }
            return null;
        }
    }

    /// <summary>
    /// 获取模块的所有快照列表（按时间倒序）
    /// </summary>
    public List<SnapshotMetadata> GetSnapshots(string moduleId)
    {
        var result = new List<SnapshotMetadata>();
        var moduleSnapshotDir = Path.Combine(_snapshotsBaseDir, moduleId);

        if (!Directory.Exists(moduleSnapshotDir))
            return result;

        foreach (var dir in Directory.GetDirectories(moduleSnapshotDir).OrderByDescending(d => d))
        {
            var metadataPath = Path.Combine(dir, "metadata.json");
            if (!File.Exists(metadataPath)) continue;

            try
            {
                var json = File.ReadAllText(metadataPath);
                var meta = JsonSerializer.Deserialize<SnapshotMetadata>(json, JsonOptions);
                if (meta != null) result.Add(meta);
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// 从快照恢复模块文件
    /// </summary>
    /// <param name="moduleId">模块 ID</param>
    /// <param name="snapshotVersion">快照版本（时间戳目录名）</param>
    /// <param name="targetDir">恢复到的目标目录</param>
    /// <param name="ct">取消令牌</param>
    public async Task<(bool Ok, string Message)> RestoreFromSnapshotAsync(
        string moduleId, string snapshotVersion, string targetDir, CancellationToken ct)
    {
        var snapshotDir = Path.Combine(_snapshotsBaseDir, moduleId, snapshotVersion);
        if (!Directory.Exists(snapshotDir))
            return (false, $"快照不存在: {snapshotVersion}");

        var metadataPath = Path.Combine(snapshotDir, "metadata.json");
        if (!File.Exists(metadataPath))
            return (false, "快照元数据文件损坏");

        try
        {
            var metadata = JsonSerializer.Deserialize<SnapshotMetadata>(
                await File.ReadAllTextAsync(metadataPath, ct), JsonOptions);
            if (metadata == null)
                return (false, "快照元数据解析失败");

            // 1) 恢复文件
            if (!string.IsNullOrEmpty(metadata.FilesArchivePath) && File.Exists(metadata.FilesArchivePath))
            {
                // 清理当前目录
                if (Directory.Exists(targetDir))
                {
                    try { Directory.Delete(targetDir, true); } catch { }
                }

                Directory.CreateDirectory(targetDir);
                // P1-3：快照解压同样走 SafeZipExtractor，避免被构造的快照包路径穿越
                SafeZipExtractor.ExtractToDirectory(metadata.FilesArchivePath, targetDir, overwrite: false);
                _logger.LogInformation("模块 {ModuleId} 文件已从快照 {Version} 恢复", moduleId, snapshotVersion);
            }

            // 2) 恢复配置
            if (!string.IsNullOrEmpty(metadata.ConfigSnapshotPath) && Directory.Exists(metadata.ConfigSnapshotPath))
            {
                var configDir = Path.Combine(targetDir, "server", "config");
                if (Directory.Exists(configDir))
                    Directory.Delete(configDir, true);

                CopyDirectory(metadata.ConfigSnapshotPath, configDir);
                _logger.LogInformation("模块 {ModuleId} 配置已从快照恢复", moduleId);
            }

            return (true, $"模块 {moduleId} 已从快照 {snapshotVersion} 恢复");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模块 {ModuleId} 快照恢复失败", moduleId);
            return (false, $"快照恢复失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理超出数量限制的旧快照
    /// </summary>
    private Task CleanupOldSnapshotsAsync(string moduleId, CancellationToken ct)
    {
        var moduleSnapshotDir = Path.Combine(_snapshotsBaseDir, moduleId);
        if (!Directory.Exists(moduleSnapshotDir)) return Task.CompletedTask;

        var dirs = Directory.GetDirectories(moduleSnapshotDir)
            .OrderByDescending(d => d)
            .Skip(MaxSnapshots)
            .ToList();

        foreach (var dir in dirs)
        {
            try
            {
                Directory.Delete(dir, true);
                _logger.LogInformation("已清理旧快照: {Dir}", dir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理旧快照失败: {Dir}", dir);
            }
        }

        return Task.CompletedTask;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
    }
}
