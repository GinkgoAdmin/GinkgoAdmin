using System.Linq.Expressions;
using Ginkgo.Domain.Logs;

namespace Ginkgo.Domain.Repositories;

/// <summary>
/// 操作日志仓储接口（DDD 领域层契约）。
/// </summary>
public interface IOpLogRepository
{
    Task<OpLog?> GetByIdAsync(long id, CancellationToken ct = default);
    Task AppendAsync(OpLog entity, CancellationToken ct = default);
    Task<List<OpLog>> ListAsync(
        int page, int pageSize,
        long? userId = null,
        long? departmentId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? action = null,
        string? resource = null,
        string? moduleLike = null,
        string? featureLike = null,
        string? type = null,
        string? keyword = null,
        CancellationToken ct = default);
    Task<long> CountAsync(
        long? userId = null,
        long? departmentId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? action = null,
        string? resource = null,
        string? moduleLike = null,
        string? featureLike = null,
        string? type = null,
        string? keyword = null,
        CancellationToken ct = default);
}
