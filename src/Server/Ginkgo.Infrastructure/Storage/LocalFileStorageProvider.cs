// 文件功能说明：
// 本地文件存储提供者实现，按日期分目录保存文件，提供保存/读取/删除功能。

namespace Ginkgo.Infrastructure.Storage;

/// <summary>
/// 本地文件存储提供者。
/// </summary>
public sealed class LocalFileStorageProvider : IFileStorageProvider
{
    private readonly string _root;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="root">根目录路径。</param>
    public LocalFileStorageProvider(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var datePath = DateTime.Now.ToString("yyyy/MM/dd");
        var dir = Path.Combine(_root, datePath);
        Directory.CreateDirectory(dir);
        var safe = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        var fileRel = Path.Combine(datePath, $"{Ginkgo.Domain.Utils.SequentialGuid.NewGuid()}_{safe}");
        var path = Path.Combine(dir, Path.GetFileName(fileRel));
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
        return fileRel.Replace('\\', '/');
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var full = Path.IsPathRooted(storagePath) ? storagePath : Path.Combine(_root, storagePath.Replace('/', Path.DirectorySeparatorChar));
        Stream s = File.OpenRead(full);
        return Task.FromResult(s);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var full = Path.IsPathRooted(storagePath) ? storagePath : Path.Combine(_root, storagePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}


