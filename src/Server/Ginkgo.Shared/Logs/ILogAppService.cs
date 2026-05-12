using Ginkgo.Shared;

namespace Ginkgo.Application.Logs;

/// <summary>
/// 日志应用服务（命令/查询用例）。
/// </summary>
public interface ILogAppService
{
    /// <summary>
    /// 追加操作日志（命令）。
    /// </summary>
    Task<long> AppendAsync(AppendOpLogInput input, CancellationToken ct = default);

    /// <summary>
    /// 分页查询操作日志（查询）。
    /// </summary>
    Task<PagedResult<OpLogListItemDto>> GetPagedAsync(ListOpLogsInput input, CancellationToken ct = default);

    /// <summary>
    /// 按 Id 获取单条日志（查询）。
    /// </summary>
    Task<OpLogListItemDto?> GetAsync(long id, CancellationToken ct = default);
}
