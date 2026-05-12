// 文件功能说明：
// 定义文件存储提供者接口，抽象保存/读取/删除三个基础操作，便于切换本地/云存储实现。

namespace Ginkgo.Infrastructure.Storage;

/// <summary>
/// 文件存储提供者接口。
/// </summary>
public interface IFileStorageProvider
{
    /// <summary>
    /// 保存文件内容。
    /// </summary>
    /// <param name="content">文件内容流。</param>
    /// <param name="fileName">原始文件名。</param>
    /// <param name="contentType">内容类型。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>返回存储路径。</returns>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// 打开只读流读取文件内容。
    /// </summary>
    /// <param name="storagePath">存储路径。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>文件流。</returns>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// 删除存储中的文件。
    /// </summary>
    /// <param name="storagePath">存储路径。</param>
    /// <param name="ct">取消令牌。</param>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

/// <summary>
/// 可提供公网直链 URL 的存储提供者（例如：对象存储）。
/// </summary>
public interface IPublicUrlProvider
{
	/// <summary>公网访问基础地址（如 https://cdn.example.com），用于客户端拼接完整 URL。</summary>
	string PublicBaseUrl { get; }

	/// <summary>根据存储 key 返回相对路径（URI 编码后的路径部分，不含域名）。</summary>
	string GetPublicUrl(string key);
}
