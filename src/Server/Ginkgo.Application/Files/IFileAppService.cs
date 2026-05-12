using Ginkgo.Shared;

namespace Ginkgo.Application.Files;

public interface IFileAppService
{
    Task<PagedResult<FileListItemDto>> GetPagedAsync(PageRequest request, string? type, long? ownerId = null, string? userName = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<FileDetailDto?> GetAsync(long id, CancellationToken ct = default);
    Task<long> CreateAsync(UploadFileInput input, long? operatorId, CancellationToken ct = default);
    /// <summary>
    /// 批量迁移文件到目标存储提供者。
    /// </summary>
    Task<int> BatchMoveAsync(List<long> ids, string targetProvider, long? operatorId, CancellationToken ct = default);
    /// <summary>
    /// 删除单个文件（含物理文件）。
    /// 返回值：true=删除成功，false=文件不存在或无权限。
    /// </summary>
    Task<bool> DeleteAsync(long id, long? operatorId, bool isAdmin, CancellationToken ct = default);
    /// <summary>
    /// 批量删除文件（含物理文件）。
    /// </summary>
    Task<int> BatchDeleteAsync(List<long> ids, long? operatorId, bool isAdmin, CancellationToken ct = default);
}

public sealed class UploadFileInput
{
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public Stream Content { get; set; } = Stream.Null;
    public string? Type { get; set; } = "default";
    public string? Tags { get; set; }
}


