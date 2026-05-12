using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ginkgo.Domain.Files;

namespace Ginkgo.Infrastructure.Storage;

/// <summary>
/// 基础设施适配器：将现有 IFileStorageProvider 适配为领域端口 IFileContentStorage。
/// </summary>
public sealed class FileContentStorageAdapter : IFileContentStorage
{
    private readonly IFileStorageProvider _inner;

    public FileContentStorageAdapter(IFileStorageProvider inner)
    {
        _inner = inner;
    }

    public string ProviderName => _inner.GetType().Name.Replace("Provider", string.Empty);

    public Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
        => _inner.SaveAsync(content, fileName, contentType, ct);

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
        => _inner.OpenReadAsync(storagePath, ct);

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
        => _inner.DeleteAsync(storagePath, ct);
}

