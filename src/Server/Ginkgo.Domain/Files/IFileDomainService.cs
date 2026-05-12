using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Files;

/// <summary>
/// 领域服务：处理文件上传中的领域规则与存储交互（哈希、校验、保存、元数据构建）。
/// </summary>
public interface IFileDomainService
{
    /// <summary>
    /// 基于上传内容创建文件实体（保存到内容存储并返回聚合根）。
    /// 不负责对外URL构建与持久化，应用服务负责编排。
    /// </summary>
    Task<SysFile> CreateFromUploadAsync(Stream content, string fileName, string? contentType, long size,
        long? ownerId, string? type, string? tags, CancellationToken ct = default);
}

