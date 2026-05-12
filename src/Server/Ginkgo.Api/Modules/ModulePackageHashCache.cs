using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 模块包 SHA256 哈希缓存。
/// 按 (PackagePath, LastWriteTimeUtc, FileLength) 作为缓存键，文件未变更时直接返回缓存值，避免每次下载都重算。
/// 由 ModulesController.GetPackage / WPF 客户端校验链路（P0-6）使用。
/// </summary>
internal static class ModulePackageHashCache
{
    private sealed record CacheEntry(string Sha256Hex, DateTime LastWriteTimeUtc, long Length);

    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取指定文件的 SHA256（十六进制小写字符串）。文件不存在抛 FileNotFoundException，由调用方处理。
    /// </summary>
    public static string GetOrCompute(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));
        var fi = new FileInfo(filePath);
        if (!fi.Exists)
            throw new FileNotFoundException("模块包文件不存在", filePath);

        if (_cache.TryGetValue(filePath, out var cached)
            && cached.LastWriteTimeUtc == fi.LastWriteTimeUtc
            && cached.Length == fi.Length)
        {
            return cached.Sha256Hex;
        }

        using var fs = fi.OpenRead();
        var bytes = SHA256.HashData(fs);
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();

        _cache[filePath] = new CacheEntry(hex, fi.LastWriteTimeUtc, fi.Length);
        return hex;
    }
}
