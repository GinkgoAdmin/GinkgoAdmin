namespace Ginkgo.Application.Files;

public sealed class FileListItemDto
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public string? StorageProvider { get; set; }
    public string? Url { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    // 用户信息
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
}

public sealed class FileDetailDto
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public string? Hash { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? DownloadUrl { get; set; }
    public long? OwnerId { get; set; }
    public string? Tags { get; set; }
    public int Version { get; set; }
    public string? Type { get; set; }
    public long? DepartmentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}


