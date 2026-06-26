using System.Text.Json;

namespace Ginkgo.Shared;

/// <summary>
/// 插件配置存储方式辅助工具（读取 module.json 中的 config 段）。
/// </summary>
public static class ModuleConfigStorageHelper
{
    public const string StorageFile = "file";
    public const string StorageDatabase = "database";

    /// <summary>
    /// 从 module.json 路径读取配置存储方式，默认 file。
    /// </summary>
    public static string GetStorageMode(string moduleJsonPath)
    {
        if (!File.Exists(moduleJsonPath)) return StorageFile;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(moduleJsonPath));
            if (doc.RootElement.TryGetProperty("config", out var cfg)
                && cfg.TryGetProperty("storage", out var storage))
            {
                var mode = storage.GetString();
                if (string.Equals(mode, StorageDatabase, StringComparison.OrdinalIgnoreCase))
                    return StorageDatabase;
            }
        }
        catch { }
        return StorageFile;
    }

    /// <summary>
    /// 判断是否为数据库存储模式。
    /// </summary>
    public static bool IsDatabaseStorage(string moduleJsonPath)
        => string.Equals(GetStorageMode(moduleJsonPath), StorageDatabase, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 读取 module.json 中声明的主配置文件名。
    /// </summary>
    public static string? GetPrimaryConfigFile(string moduleJsonPath)
    {
        if (!File.Exists(moduleJsonPath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(moduleJsonPath));
            if (doc.RootElement.TryGetProperty("config", out var cfg)
                && cfg.TryGetProperty("primaryFile", out var pf))
                return pf.GetString();
        }
        catch { }
        return null;
    }
}
