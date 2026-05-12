using System.IO.Compression;

namespace Ginkgo.Api.Modules;

/// <summary>
/// 安全解压工具，针对模块包/快照包提供 ZipSlip 防御。
/// 设计要点（P1-3）：
/// 1. 解压前先用 <see cref="ZipArchive"/> 流式枚举所有 entry，逐项检查 <c>entry.FullName</c>，
///    不接受绝对路径、不接受包含 ".." 的相对段，目标绝对路径必须严格落在解压目录之下；
/// 2. 任何一项不合法，整个解压立即终止并抛 <see cref="InvalidDataException"/>，调用方负责清理目录；
/// 3. 单条 entry 大小、总解压大小、entry 数量均设上限，避免 zip-bomb；
/// 4. 不主动跟随符号链接，按字节流写入文件；
/// 5. 仅作"传输层"安全保证，业务层（白名单扩展名、哈希、签名）由 <c>ModuleUploadService</c> 等继续负责。
/// </summary>
internal static class SafeZipExtractor
{
    /// <summary>单条 entry 解压后允许的最大字节数（默认 256 MiB）。</summary>
    private const long DefaultMaxEntryBytes = 256L * 1024 * 1024;

    /// <summary>整包解压后允许的最大字节数（默认 1 GiB）。</summary>
    private const long DefaultMaxTotalBytes = 1024L * 1024 * 1024;

    /// <summary>整包 entry 数量上限（默认 50000）。</summary>
    private const int DefaultMaxEntryCount = 50000;

    /// <summary>
    /// 替代 <c>ZipFile.ExtractToDirectory(zipPath, targetDir)</c>，提供 ZipSlip / zip-bomb 防御。
    /// </summary>
    /// <param name="zipFilePath">zip 文件路径</param>
    /// <param name="destinationDirectory">解压目标目录（必须事先创建好或可被创建）</param>
    /// <param name="overwrite">是否覆盖已存在文件</param>
    /// <param name="maxEntryBytes">单 entry 字节上限</param>
    /// <param name="maxTotalBytes">整包字节上限</param>
    /// <param name="maxEntryCount">entry 数量上限</param>
    public static void ExtractToDirectory(
        string zipFilePath,
        string destinationDirectory,
        bool overwrite = false,
        long maxEntryBytes = DefaultMaxEntryBytes,
        long maxTotalBytes = DefaultMaxTotalBytes,
        int maxEntryCount = DefaultMaxEntryCount)
    {
        if (string.IsNullOrEmpty(zipFilePath))
            throw new ArgumentNullException(nameof(zipFilePath));
        if (string.IsNullOrEmpty(destinationDirectory))
            throw new ArgumentNullException(nameof(destinationDirectory));

        Directory.CreateDirectory(destinationDirectory);
        var fullDestRoot = Path.GetFullPath(destinationDirectory);
        var rootWithSep = fullDestRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullDestRoot
            : fullDestRoot + Path.DirectorySeparatorChar;

        using var zip = ZipFile.OpenRead(zipFilePath);

        if (zip.Entries.Count > maxEntryCount)
            throw new InvalidDataException($"zip 包含 {zip.Entries.Count} 个 entry，超过上限 {maxEntryCount}（疑似 zip-bomb）");

        long totalUncompressed = 0;

        foreach (var entry in zip.Entries)
        {
            var rawName = entry.FullName;
            if (string.IsNullOrEmpty(rawName))
                continue;

            // 拒绝绝对路径与盘符前缀
            if (Path.IsPathRooted(rawName) || rawName.Contains(':'))
                throw new InvalidDataException($"zip entry 包含非法绝对路径: {rawName}");

            // 统一分隔符并拒绝任何 ".." 段
            var normalized = rawName.Replace('\\', '/');
            foreach (var seg in normalized.Split('/'))
            {
                if (seg == "..")
                    throw new InvalidDataException($"zip entry 包含路径穿越段 '..': {rawName}");
            }

            // 目录 entry：以 / 结尾且无文件名
            var isDirectoryEntry = string.IsNullOrEmpty(entry.Name)
                || rawName.EndsWith('/') || rawName.EndsWith('\\');

            // 计算最终绝对路径，并强制落在解压根目录下
            var combined = Path.Combine(fullDestRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            var fullEntryPath = Path.GetFullPath(combined);

            if (isDirectoryEntry)
            {
                if (!fullEntryPath.Equals(fullDestRoot, StringComparison.OrdinalIgnoreCase)
                    && !fullEntryPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"zip entry 解析后逃逸出目标目录: {rawName}");
                }
                Directory.CreateDirectory(fullEntryPath);
                continue;
            }

            if (!fullEntryPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"zip entry 解析后逃逸出目标目录: {rawName}");

            // zip-bomb 防御：单条与累计字节上限
            if (entry.Length < 0)
                throw new InvalidDataException($"zip entry 长度非法: {rawName}");
            if (entry.Length > maxEntryBytes)
                throw new InvalidDataException($"zip entry '{rawName}' 解压长度 {entry.Length} 超过上限 {maxEntryBytes}（疑似 zip-bomb）");

            totalUncompressed += entry.Length;
            if (totalUncompressed > maxTotalBytes)
                throw new InvalidDataException($"zip 累计解压大小超过上限 {maxTotalBytes}（疑似 zip-bomb）");

            var parentDir = Path.GetDirectoryName(fullEntryPath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);

            var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;
            using var src = entry.Open();
            using var dest = new FileStream(fullEntryPath, fileMode, FileAccess.Write, FileShare.None);
            src.CopyTo(dest);
        }
    }
}
