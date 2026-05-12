using System.Security.Cryptography;

namespace Ginkgo.Domain.Files;

/// <summary>
/// 领域服务实现：封装上传内容的校验、哈希、存储与元数据创建。
/// </summary>
public sealed class FileDomainService : IFileDomainService
{
    private readonly IFileContentStorage _storage;

    public FileDomainService(IFileContentStorage storage)
    {
        _storage = storage;
    }

    public async Task<SysFile> CreateFromUploadAsync(Stream content, string fileName, string? contentType, long size,
        long? ownerId, string? type, string? tags, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("文件名不能为空", nameof(fileName));
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));

        // 推断 ContentType（与应用层逻辑一致，保证在缺省时给个合理值）
        contentType = NormalizeContentType(fileName, contentType);

        // 计算 SHA-256 哈希（可选失败不致命）
        string? hash = null;
        try
        {
            content.Position = 0;
            using var sha256 = SHA256.Create();
            var h = await sha256.ComputeHashAsync(content, ct);
            hash = BitConverter.ToString(h).Replace("-", string.Empty).ToLowerInvariant();
        }
        catch { }

        // 存储
        content.Position = 0;
        var storagePath = await _storage.SaveAsync(content, fileName, contentType!, ct);

        // 构建聚合根并附加存储信息
        var entity = SysFile.CreateNew(fileName, contentType!, size, ownerId, type, tags, hash);
        entity.AttachStorage(_storage.ProviderName, storagePath);

        return entity;
    }

    private static string NormalizeContentType(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType)) return contentType;
        var ext = (System.IO.Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}

