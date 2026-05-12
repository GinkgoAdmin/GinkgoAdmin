using System.Text.Json;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 管理待删除的模块目录（因 DLL 被锁定无法立即删除）
/// 在应用启动时清理这些目录
/// </summary>
public sealed class PendingDeleteManager
{
    private readonly string _pendingFilePath;
    private readonly ILogger<PendingDeleteManager> _logger;
    private readonly object _lock = new();

    public PendingDeleteManager(ILogger<PendingDeleteManager> logger)
    {
        _logger = logger;
        _pendingFilePath = Path.Combine(AppContext.BaseDirectory, "pending_delete.json");
    }

    /// <summary>
    /// 添加待删除目录
    /// </summary>
    public void AddPendingDelete(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return;
        
        lock (_lock)
        {
            var list = LoadPendingList();
            if (!list.Contains(directoryPath, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(directoryPath);
                SavePendingList(list);
                _logger.LogInformation("[PendingDeleteManager] 已添加待删除目录: {Path}", directoryPath);
            }
        }
    }

    /// <summary>
    /// 检查指定模块是否有待删除目录
    /// </summary>
    public bool HasPendingDeleteForModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return false;
        
        lock (_lock)
        {
            var list = LoadPendingList();
            return list.Any(p => p.Contains(moduleId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 获取指定模块的待删除目录列表
    /// </summary>
    public List<string> GetPendingDeletesForModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return new List<string>();
        
        lock (_lock)
        {
            var list = LoadPendingList();
            return list.Where(p => p.Contains(moduleId, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    /// <summary>
    /// 获取所有待删除目录
    /// </summary>
    public List<string> GetAllPendingDeletes()
    {
        lock (_lock)
        {
            return LoadPendingList();
        }
    }

    /// <summary>
    /// 清理所有待删除目录（在应用启动时调用）
    /// </summary>
    public void CleanupPendingDeletes()
    {
        var remaining = CleanupPendingDeletes(AppContext.BaseDirectory, message => _logger.LogInformation("{Message}", message));
        _logger.LogInformation("[PendingDeleteManager] 清理完成，剩余 {Count} 个待删除目录", remaining.Count);
    }

    /// <summary>
    /// 在模块预加载前清理待删除目录，避免残留 DLL 被再次加载后继续锁定。
    /// </summary>
    public static List<string> CleanupPendingDeletes(string baseDirectory, Action<string>? log = null)
    {
        var pendingFilePath = Path.Combine(baseDirectory, "pending_delete.json");
        var list = LoadPendingList(pendingFilePath);
        if (list.Count == 0) return list;

        log?.Invoke($"[PendingDeleteManager] 开始清理 {list.Count} 个待删除目录");

        var remaining = new List<string>();
        foreach (var path in list)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                    }

                    Directory.Delete(path, recursive: true);
                    log?.Invoke($"[PendingDeleteManager] 已删除目录: {path}");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"[PendingDeleteManager] 删除目录失败，保留待下次清理: {path}，原因：{ex.Message}");
                remaining.Add(path);
            }
        }

        SavePendingList(pendingFilePath, remaining);
        return remaining;
    }

    private List<string> LoadPendingList()
    {
        return LoadPendingList(_pendingFilePath);
    }

    private static List<string> LoadPendingList(string pendingFilePath)
    {
        try
        {
            if (File.Exists(pendingFilePath))
            {
                var json = File.ReadAllText(pendingFilePath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
        }
        catch
        {
        }
        return new List<string>();
    }

    private void SavePendingList(List<string> list)
    {
        SavePendingList(_pendingFilePath, list);
    }

    private static void SavePendingList(string pendingFilePath, List<string> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(pendingFilePath, json);
        }
        catch
        {
        }
    }
}
