using Ginkgo.Shared;

namespace Ginkgo.Application.Notifications;

public interface INotifyAppService
{
    Task<long> CreateAsync(CreateNotifyInput input, CancellationToken ct = default);
        Task UpdateAsync(long id, UpdateNotifyInput input, CancellationToken ct = default);
        Task<CreateNotifyInput?> GetDetailAsync(long id, CancellationToken ct = default);

    Task PublishAsync(long notifyId, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);

    Task<PagedResult<NotifyListItemDto>> GetPagedAsync(PageRequest request, CancellationToken ct = default);

    // 我的通知
    Task<List<MyNotifyListItemDto>> GetMyListAsync(long userId, CancellationToken ct = default);
    Task<MyNotifyDetailDto?> GetMyDetailAsync(long userId, long notifyId, CancellationToken ct = default);
    Task MarkReadAsync(long userId, long notifyId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default);

    // 统计：发送人数、已送达人数、已送达名单、未读取名单
    Task<NotifyStatsDto> GetStatsAsync(long notifyId, CancellationToken ct = default);

	    // 新增：统计摘要（限制前10个，用于列表视图）
	    Task<NotifyStatsSummaryDto> GetStatsSummaryAsync(long notifyId, CancellationToken ct = default);

	    // 新增：已发布详情（含附件与名单分页/搜索）
	    Task<PublishedNotifyDetailDto?> GetPublishedDetailAsync(long notifyId, string? listType = "unread", string? keyword = null, int page = 1, int pageSize = 50, CancellationToken ct = default);

	    // 新增：软删除（已发布）
	    Task SoftDeleteAsync(long notifyId, CancellationToken ct = default);

}


