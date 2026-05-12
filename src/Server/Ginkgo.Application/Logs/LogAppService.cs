using System.Linq;
using Ginkgo.Domain; // IRepository<>, IUnitOfWork
using Ginkgo.Domain.Events;
using Ginkgo.Domain.Logs;
using Ginkgo.Domain.Logs.Events;
using Ginkgo.Domain.Repositories;
using Ginkgo.Domain.Users;
using Ginkgo.Shared;
using System.Text.Json;


namespace Ginkgo.Application.Logs;

/// <summary>
/// 操作日志应用服务（CQRS：命令 Append + 查询 Get/List）。
/// 说明：保持与现有 LogsController 兼容，不强制替换 Controller；后续可逐步切换为调用本服务。
/// </summary>
public sealed class LogAppService : ILogAppService
{
    private readonly IOpLogRepository _oplogRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDomainEventPublisher _bus;

    public LogAppService(IOpLogRepository oplogRepo, IRepository<User> userRepo, IUnitOfWork uow, IDomainEventPublisher bus)
    { _oplogRepo = oplogRepo; _userRepo = userRepo; _uow = uow; _bus = bus; }

    /// <inheritdoc />
    public async Task<long> AppendAsync(AppendOpLogInput input, CancellationToken ct = default)
    {
        // 基本入参校验（领域规则更复杂时可下沉到聚合方法）
        if (string.IsNullOrWhiteSpace(input.Action)) throw new ArgumentException("Action 不能为空", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Resource)) throw new ArgumentException("Resource 不能为空", nameof(input));

        var now = input.CreatedAt ?? DateTime.Now;
        var entity = new OpLog
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            Action = input.Action.Trim(),
            Resource = input.Resource.Trim(),
            Result = string.IsNullOrWhiteSpace(input.Result) ? "OK" : input.Result!.Trim(),
            ElapsedMs = input.ElapsedMs,
            DataJson = input.DataJson,
            DepartmentId = input.DepartmentId,
            Ip = input.Ip,
            UserAgent = input.UserAgent,
            UserId = input.CreatedBy,
            ModuleCN = input.ModuleCN,
            FeatureCN = input.FeatureCN,
            ReviewCN = input.ReviewCN,
            At = now,  // 操作时间（数据库 NOT NULL）
            CreatedAt = now,
            CreatedBy = input.CreatedBy,
            IsDeleted = false,
            DeletedAt = null,
            DeletedBy = null
        };

        await _uow.BeginAsync(ct);
        await _oplogRepo.AppendAsync(entity, ct);
        await _uow.CommitAsync(ct);
        await _bus.PublishAsync(new OpLogAppended(entity.Id), ct);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<PagedResult<OpLogListItemDto>> GetPagedAsync(ListOpLogsInput input, CancellationToken ct = default)
    {
        var page = input.Page <= 0 ? 1 : input.Page;
        var size = input.PageSize <= 0 ? 20 : input.PageSize;
        var list = await _oplogRepo.ListAsync(page, size, input.UserId, input.DepartmentId, input.From, input.To, input.Action, input.Resource, input.Module, input.Feature, input.Type, input.Keyword, ct);
        var total = await _oplogRepo.CountAsync(input.UserId, input.DepartmentId, input.From, input.To, input.Action, input.Resource, input.Module, input.Feature, input.Type, input.Keyword, ct);

        // 附带用户显示信息（与现有 LogsController 保持一致做法）
        var uids = list.Where(x => x.CreatedBy != null).Select(x => x.CreatedBy!.Value).Distinct().ToList();
        var usersQuery = _userRepo.Query().Where(u => uids.Contains(u.Id));
        // 关键字过滤（对用户信息做模糊匹配）
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var kw = input.Keyword.Trim();
            usersQuery = usersQuery.Where(u =>
                (u.UserName != null && u.UserName.Contains(kw)) ||
                (u.DisplayName != null && u.DisplayName.Contains(kw)) ||
                (u.Email != null && u.Email.Contains(kw)) ||
                (u.Phone != null && u.Phone.Contains(kw))
            );
        }
        var usersList = usersQuery
            .Select(u => new { u.Id, u.UserName, u.DisplayName, u.Email, u.Phone })
            .ToList();
        var users = usersList.ToDictionary(u => u.Id, u => new { u.UserName, u.DisplayName, u.Email, u.Phone });

        var items = list.Select(x => new OpLogListItemDto
        {
            Id = x.Id,
            UserId = x.CreatedBy,
            Action = x.Action,
            Resource = x.Resource,
            Ip = x.Ip,
            UserAgent = x.UserAgent ?? TryGetUserAgent(x.DataJson),
            Result = x.Result,
            ElapsedMs = x.ElapsedMs,
            DataJson = x.DataJson,
            ModuleCN = x.ModuleCN,
            FeatureCN = x.FeatureCN,
            ReviewCN = x.ReviewCN,
            DepartmentId = x.DepartmentId,
            At = x.At,  // 操作时间（兼容旧版 WPF 客户端）
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy,
            IsDeleted = x.IsDeleted,
            DeletedAt = x.DeletedAt,
            DeletedBy = x.DeletedBy,
            UserName = x.CreatedBy != null && users.TryGetValue(x.CreatedBy.Value, out var u) ? u.UserName : null,
            DisplayName = x.CreatedBy != null && users.TryGetValue(x.CreatedBy.Value, out var u2) ? u2.DisplayName : null,
            Email = x.CreatedBy != null && users.TryGetValue(x.CreatedBy.Value, out var u3) ? u3.Email : null,
            Phone = x.CreatedBy != null && users.TryGetValue(x.CreatedBy.Value, out var u4) ? u4.Phone : null
        }).ToList();

        // 若指定了关键字但筛选后无匹配的用户，items 需要据此再做一次过滤（以用户维度过滤）
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var kw = input.Keyword.Trim().ToLowerInvariant();
            items = items.Where(it =>
                (!string.IsNullOrEmpty(it.UserName) && it.UserName!.ToLowerInvariant().Contains(kw)) ||
                (!string.IsNullOrEmpty(it.DisplayName) && it.DisplayName!.ToLowerInvariant().Contains(kw)) ||
                (!string.IsNullOrEmpty(it.Email) && it.Email!.ToLowerInvariant().Contains(kw)) ||
                (!string.IsNullOrEmpty(it.Phone) && it.Phone!.ToLowerInvariant().Contains(kw))
            ).ToList();
        }

        return new PagedResult<OpLogListItemDto> { Total = total, Page = page, PageSize = size, Items = items };
    }

    /// <inheritdoc />
    public async Task<OpLogListItemDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var x = await _oplogRepo.GetByIdAsync(id, ct);
        if (x == null) return null;
        var dto = new OpLogListItemDto
        {
            Id = x.Id,
            UserId = x.CreatedBy,
            Action = x.Action,
            Resource = x.Resource,
            Ip = x.Ip,
            UserAgent = x.UserAgent ?? TryGetUserAgent(x.DataJson),
            Result = x.Result,
            ElapsedMs = x.ElapsedMs,
            DataJson = x.DataJson,
            ModuleCN = x.ModuleCN,
            FeatureCN = x.FeatureCN,
            ReviewCN = x.ReviewCN,
            DepartmentId = x.DepartmentId,
            At = x.At,  // 操作时间（兼容旧版 WPF 客户端）
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy,
            IsDeleted = x.IsDeleted,
            DeletedAt = x.DeletedAt,
            DeletedBy = x.DeletedBy
        };
        if (x.CreatedBy != null)
        {
            var u = _userRepo.Query().Where(u => u.Id == x.CreatedBy).Select(u => new { u.UserName, u.DisplayName }).FirstOrDefault();
            if (u != null) { dto.UserName = u.UserName; dto.DisplayName = u.DisplayName; }
        }
        return dto;
    }

        private static string? TryGetUserAgent(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("userAgent", out var p))
                    return p.GetString();
            }
            catch { }
            return null;
        }

}
