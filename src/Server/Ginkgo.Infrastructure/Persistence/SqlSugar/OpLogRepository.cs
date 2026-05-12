using Ginkgo.Domain.Logs;
using Ginkgo.Domain.Repositories;
using SqlSugar;

namespace Ginkgo.Infrastructure.Persistence.SqlSugar;

/// <summary>
/// 操作日志仓储（SqlSugar 实现）。
/// 说明：为保持与现有 LogsController 兼容，读取侧仍可通过通用 IRepository<OpLog>；
/// 本仓储用于 DDD/CQRS 的 Application 命令/查询处理器。
/// </summary>
public sealed class OpLogRepository : IOpLogRepository
{
    private readonly ISqlSugarClient _db;
    public OpLogRepository(ISqlSugarClient db) => _db = db;

    public async Task<OpLog?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _db.Queryable<OpLog>().Where(x => x.Id == id && !x.IsDeleted).FirstAsync();

    public async Task AppendAsync(OpLog entity, CancellationToken ct = default)
        => await _db.Insertable(entity).ExecuteCommandAsync();

    public async Task<long> CountAsync(
        long? userId = null, long? departmentId = null,
        DateTime? from = null, DateTime? to = null,
        string? action = null, string? resource = null,
        string? moduleLike = null, string? featureLike = null, string? type = null, string? keyword = null,
        CancellationToken ct = default)
    {
        var q = BuildQuery(userId, departmentId, from, to, action, resource, moduleLike, featureLike, type, keyword);
        return await q.CountAsync();
    }

    public async Task<List<OpLog>> ListAsync(
        int page, int pageSize,
        long? userId = null, long? departmentId = null,
        DateTime? from = null, DateTime? to = null,
        string? action = null, string? resource = null,
        string? moduleLike = null, string? featureLike = null, string? type = null, string? keyword = null,
        CancellationToken ct = default)
    {
        if (page <= 0) page = 1; if (pageSize <= 0) pageSize = 20;
        var q = BuildQuery(userId, departmentId, from, to, action, resource, moduleLike, featureLike, type, keyword)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        return await q.ToListAsync();
    }

    private ISugarQueryable<OpLog> BuildQuery(
        long? userId, long? departmentId,
        DateTime? from, DateTime? to,
        string? action, string? resource,
        string? moduleLike, string? featureLike, string? type, string? keyword)
    {
        var q = _db.Queryable<OpLog>().Where(x => !x.IsDeleted);
        if (userId != null) q = q.Where(x => x.CreatedBy == userId);
        if (departmentId != null) q = q.Where(x => x.DepartmentId == departmentId);
        if (from != null) q = q.Where(x => x.CreatedAt >= from);
        if (to != null) q = q.Where(x => x.CreatedAt <= to);
        
        // Action 参数放宽匹配范围：匹配 Action 或 功能模块中文名 或 审核结果（如'登录'能匹配到，'上传'能匹配到）
        if (!string.IsNullOrWhiteSpace(action)) 
            q = q.Where(x => x.Action == action || x.FeatureCN!.Contains(action) || x.ReviewCN!.Contains(action));
            
        if (!string.IsNullOrWhiteSpace(resource)) q = q.Where(x => x.Resource == resource);
        if (!string.IsNullOrWhiteSpace(moduleLike)) q = q.Where(x => x.ModuleCN!.Contains(moduleLike));
        if (!string.IsNullOrWhiteSpace(featureLike)) q = q.Where(x => x.FeatureCN!.Contains(featureLike));

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToLowerInvariant();
            if (normalizedType == "normal")
            {
                q = q.Where(x =>
                    x.Result == "OK" ||
                    x.Result == "Ok" ||
                    x.Result == "ok" ||
                    x.Result == "SUCCESS" ||
                    x.Result == "Success" ||
                    x.Result == "success" ||
                    x.Result!.Contains("成功") ||
                    x.ReviewCN!.Contains("成功"));
            }
            else if (normalizedType == "error")
            {
                q = q.Where(x =>
                    x.Result == "ERROR" ||
                    x.Result == "Error" ||
                    x.Result == "error" ||
                    x.Result == "FAIL" ||
                    x.Result == "Fail" ||
                    x.Result == "fail" ||
                    x.Result == "EXCEPTION" ||
                    x.Result == "Exception" ||
                    x.Result == "exception" ||
                    x.Result!.Contains("失败") ||
                    x.Result!.Contains("异常") ||
                    x.ReviewCN!.Contains("失败") ||
                    x.ReviewCN!.Contains("异常"));
            }
        }
        
        // 关键字匹配：查 Result、DataJson、审查中文备注等
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(x => x.Result!.Contains(kw) || x.ReviewCN!.Contains(kw) || x.DataJson!.Contains(kw) || x.FeatureCN!.Contains(kw));
        }

        return q;
    }
}
