using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ginkgo.Domain.Files;

/// <summary>
/// 领域层存储端口：文件内容读写删除。
/// 由基础设施层适配具体实现（本地/云）。
/// </summary>
public interface IFileContentStorage
{
    /// <summary>提供者名称（用于元数据记录，如 Local、Oss 等）。</summary>
    string ProviderName { get; }

    /// <summary>保存内容，返回存储路径/Key。</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);

    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

