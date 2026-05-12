using Ginkgo.Domain.Files;

namespace Ginkgo.Application.Files;

/// <summary>
/// 文件公开授权服务接口。
/// 负责管理文件的公开访问 Grant（创建、校验、撤销）。
/// </summary>
public interface IFileGrantService
{
    /// <summary>
    /// 为指定文件确保存在一个有效的公开授权。
    /// 如果同一 (fileId, refType, refId, fieldName) 已存在未撤销的 grant，直接返回其 publicUrl。
    /// 否则创建新 grant 并返回 publicUrl。
    /// </summary>
    /// <param name="fileId">文件Id</param>
    /// <param name="refType">引用方类型（如 Setting、Article）</param>
    /// <param name="refId">引用方对象标识（如 Site.Logo）</param>
    /// <param name="fieldName">引用方字段名（如 Logo）</param>
    /// <param name="operatorId">操作人Id</param>    /// <param name="ct">取消令牌</param>    /// <returns>公开访问 URL，格式如 /api/v1/files/public/{grantKey}</returns>
    Task<string> EnsurePublicGrantAsync(long fileId, string refType, string refId,
        string? fieldName = null, long? operatorId = null, CancellationToken ct = default);

    /// <summary>
    /// 撤销指定引用方的所有公开授权，原公开链接立即失效。
    /// </summary>
    /// <param name="refType">引用方类型</param>
    /// <param name="refId">引用方对象标识</param>
    /// <param name="fieldName">引用方字段名（null 表示撤销该 refType+refId 下的所有字段）</param>
    /// <param name="operatorId">操作人 Id</param>
    /// <param name="ct">取消令牌</param>
    Task RevokeGrantsAsync(string refType, string refId, string? fieldName = null,
        long? operatorId = null, CancellationToken ct = default);

    /// <summary>
    /// 校验 grantKey 是否有效（未撤销、未过期、未软删）。
    /// 有效时返回 Grant 实体；无效返回 null。
    /// </summary>
    Task<SysFileGrant?> ValidateGrantAsync(string grantKey, CancellationToken ct = default);
}
