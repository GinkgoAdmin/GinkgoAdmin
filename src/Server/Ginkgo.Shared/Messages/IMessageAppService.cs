// 文件功能说明：
// 定义消息通知应用服务接口。

using Ginkgo.Shared;

namespace Ginkgo.Application.Messages;

/// <summary>
/// 消息通知应用服务接口。
/// </summary>
public interface IMessageAppService
{
    /// <summary>
    /// 分页查询当前用户的消息列表（按创建时间倒序）。
    /// </summary>
    /// <param name="userId">用户 Id。</param>
    /// <param name="page">页号（从 1 开始）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="isRead">已读状态筛选（null 表示不筛选）。</param>
    /// <param name="deliveryRole">送达角色筛选（null 表示不筛选）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<PagedResult<MessageListItemDto>> GetPagedListAsync(long userId, int page, int pageSize, bool? isRead = null, string? deliveryRole = null, CancellationToken ct = default);

    /// <summary>
    /// 获取单条消息详情（验证归属当前用户）。
    /// </summary>
    /// <param name="id">消息 Id。</param>
    /// <param name="userId">当前用户 Id。</param>
    /// <param name="platform">平台类型，用于过滤链接（null 表示返回所有链接）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<MessageDetailDto> GetDetailAsync(long id, long userId, string? platform = null, CancellationToken ct = default);

    /// <summary>
    /// 创建消息（支持主送 + 知会两组接收对象、附件和链接）。
    /// </summary>
    /// <param name="input">消息创建输入。</param>
    /// <param name="ct">取消令牌。</param>
    Task CreateAsync(CreateMessageInput input, CancellationToken ct = default);

    /// <summary>
    /// 标记单条消息为已读。
    /// </summary>
    /// <param name="id">消息 Id。</param>
    /// <param name="userId">当前用户 Id。</param>
    /// <param name="ct">取消令牌。</param>
    Task MarkAsReadAsync(long id, long userId, CancellationToken ct = default);

    /// <summary>
    /// 将当前用户所有未读消息标记为已读。
    /// </summary>
    /// <param name="userId">用户 Id。</param>
    /// <param name="ct">取消令牌。</param>
    Task MarkAllAsReadAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 获取当前用户的未读消息数量。
    /// </summary>
    /// <param name="userId">用户 Id。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 管理端：分页查询已发送的消息（按标题+内容+发送人+时间分组去重）。
    /// </summary>
    Task<PagedResult<AdminMessageListItemDto>> GetAdminPagedListAsync(int page, int pageSize, string? title = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);

    /// <summary>
    /// 管理端：获取某条消息的投递统计（总接收人数、已送达、已读等）。
    /// </summary>
    Task<AdminMessageStatsDto> GetAdminStatsAsync(string title, DateTime createdAt, CancellationToken ct = default);

    /// <summary>
    /// 管理端：获取消息批次详情（含正文、附件和链接）。
    /// </summary>
    Task<AdminMessageDetailDto> GetAdminDetailAsync(string title, DateTime createdAt, CancellationToken ct = default);

    /// <summary>
    /// 管理端：删除一批消息（按标题+创建时间匹配的所有记录）。
    /// </summary>
    Task DeleteBatchAsync(string title, DateTime createdAt, CancellationToken ct = default);

}
