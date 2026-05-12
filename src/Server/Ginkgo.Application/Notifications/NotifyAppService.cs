using Ginkgo.Domain;
using Ginkgo.Domain.Notifications;
using Ginkgo.Realtime;
using Ginkgo.Shared;
using SqlSugar;
using Ginkgo.Domain.Users;



namespace Ginkgo.Application.Notifications;

public sealed class NotifyAppService : INotifyAppService
{
    private readonly IRepository<NotifyMessage> _msgRepo;
    private readonly IRepository<NotifyAudienceSeed> _seedRepo;
    private readonly IRepository<NotifyAudience> _audRepo;
    private readonly IRepository<Ginkgo.Domain.Users.User> _userRepo;
    private readonly IRepository<Ginkgo.Domain.Users.UserRole> _userRoleRepo;
    private readonly IRepository<Ginkgo.Domain.Users.UserDepartment> _userDeptRepo;
    private readonly IRepository<Ginkgo.Domain.Departments.Department> _deptRepo;
    private readonly IQueuePublisher _queue;

    public NotifyAppService(
        IRepository<NotifyMessage> msgRepo,
        IRepository<NotifyAudienceSeed> seedRepo,
        IRepository<NotifyAudience> audRepo,
        IRepository<Ginkgo.Domain.Users.User> userRepo,
        IRepository<Ginkgo.Domain.Users.UserRole> userRoleRepo,
        IRepository<Ginkgo.Domain.Users.UserDepartment> userDeptRepo,
        IRepository<Ginkgo.Domain.Departments.Department> deptRepo,
        IQueuePublisher queue)
    {
        _msgRepo = msgRepo; _seedRepo = seedRepo; _audRepo = audRepo; _userRepo = userRepo; _userRoleRepo = userRoleRepo; _userDeptRepo = userDeptRepo; _deptRepo = deptRepo; _queue = queue;
    }

	    public async Task<CreateNotifyInput?> GetDetailAsync(long id, CancellationToken ct = default)
	    {
	        var m = _msgRepo.Query().FirstOrDefault(x => x.Id == id);
	        if (m == null) return null;
	        var seeds = _seedRepo.Query().Where(x => x.NotifyId == id).ToList();
	        return await Task.FromResult(new CreateNotifyInput
	        {
	            Title = m.Title,
	            ContentType = m.ContentType,
	            ContentText = m.ContentText,
	            ContentHtml = m.ContentHtml,
	            IsImportant = m.IsImportant,
	            Priority = m.Priority,
	            Audience = seeds.Select(s => new AudienceSeedInput { TargetType = s.TargetType, TargetValue = s.TargetValue }).ToList()
	        });
	    }

	    public async Task UpdateAsync(long id, UpdateNotifyInput input, CancellationToken ct = default)
	    {
	        var m = await _msgRepo.GetByIdAsync(id, ct); if (m == null) return;
	        // 仅允许编辑草稿
	        if (m.Status != 0) return;
	        m.Title = input.Title.Trim();
	        m.ContentType = input.ContentType;
	        m.ContentText = input.ContentText;
	        m.ContentHtml = input.ContentHtml;
	        m.IsImportant = input.IsImportant;
	        m.Priority = input.Priority;
	        await _msgRepo.UpdateAsync(m, ct);
	        // 如果传入了受众，则替换（未传入则保留原受众）
	        if (input.Audience != null)
	        {
	            var oldIds = _seedRepo.Query().Where(x => x.NotifyId == id).Select(x => x.Id).ToList();
	            if (oldIds.Any())
	            {
	                foreach (var oid in oldIds) await _seedRepo.DeleteAsync(oid, ct);
	            }
	            foreach (var s in input.Audience)
	            {
	                await _seedRepo.AddAsync(new NotifyAudienceSeed { NotifyId = id, TargetType = s.TargetType, TargetValue = s.TargetValue }, ct);
	            }
	        }
	    }
        public async Task DeleteAsync(long id, CancellationToken ct = default)
        {
            var m = await _msgRepo.GetByIdAsync(id, ct);
            if (m == null) return;
            if (m.Status != 0)
            {
                // 非草稿不允许删除 —— 抛出业务异常，交由全局异常处理中间件返回 400
                throw new InvalidOperationException("仅允许删除草稿状态的通知");
            }
            // 删除种子
            var seedIds = _seedRepo.Query().Where(x => x.NotifyId == id).Select(x => x.Id).ToList();
            foreach (var sid in seedIds) await _seedRepo.DeleteAsync(sid, ct);
            // 删除消息
            await _msgRepo.DeleteAsync(id, ct);
        }



    public async Task<long> CreateAsync(CreateNotifyInput input, CancellationToken ct = default)
    {
        var m = new NotifyMessage
        {
            Title = input.Title.Trim(),
            ContentType = input.ContentType,
            ContentText = input.ContentText,
            ContentHtml = input.ContentHtml,
            IsImportant = input.IsImportant,
            Priority = input.Priority,
            Status = 0
        };
        await _msgRepo.AddAsync(m, ct);
        foreach (var s in input.Audience)
        {
            await _seedRepo.AddAsync(new NotifyAudienceSeed
            {
                NotifyId = m.Id,
                TargetType = s.TargetType,
                TargetValue = s.TargetValue
            }, ct);
        }
        return m.Id;
    }

    public async Task PublishAsync(long notifyId, CancellationToken ct = default)
    {
        var msg = await _msgRepo.GetByIdAsync(notifyId, ct);
        if (msg == null) return;
        msg.Status = 2; // Publishing
        msg.PublishedAt = DateTime.Now;
        await _msgRepo.UpdateAsync(msg, ct);

        // 展开对象
        var seeds = _seedRepo.Query().Where(x => x.NotifyId == notifyId).ToList();
        var allUsers = new HashSet<long>();
        bool includeAll = seeds.Any(s => s.TargetType == 4);
        if (includeAll)
        {
            foreach (var uid in _userRepo.Query().Select(u => u.Id).ToList()) allUsers.Add(uid);
        }
        // 按用户
        foreach (var s in seeds.Where(x => x.TargetType == 1))
        {
            if (long.TryParse(s.TargetValue, out var uid)) allUsers.Add(uid);
        }
        // 按角色
        var roleIds = seeds.Where(x => x.TargetType == 2).Select(x => x.TargetValue).Select(v => long.TryParse(v, out var g) ? (long?)g : null).Where(g => g.HasValue).Select(g => g!.Value).ToList();
        if (roleIds.Count > 0)
        {
            foreach (var uid in _userRoleRepo.Query().Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToList()) allUsers.Add(uid);
        }
        // 按部门/含下级
        var deptSeeds = seeds.Where(x => x.TargetType == 3).Select(x => x.TargetValue).ToList();
        if (deptSeeds.Count > 0)
        {
            var allDepts = _deptRepo.Query().ToList();
            HashSet<long> expandDepts = new();
            foreach (var val in deptSeeds)
            {
                var parts = val.Split(':');
                if (!long.TryParse(parts[0], out var did)) continue;
                bool deep = parts.Length >= 2 && string.Equals(parts[1], "deep", StringComparison.OrdinalIgnoreCase);
                if (!deep) { expandDepts.Add(did); continue; }
                // 递归包含下级
                void walk(long id)
                {
                    if (!expandDepts.Add(id)) return;
                    foreach (var c in allDepts.Where(d => d.ParentId == id)) walk(c.Id);
                }
                walk(did);
            }
            if (expandDepts.Count > 0)
            {
                foreach (var uid in _userDeptRepo.Query().Where(ud => expandDepts.Contains(ud.DepartmentId)).Select(ud => ud.UserId).Distinct().ToList()) allUsers.Add(uid);
            }
        }

        // 写入接收人并投递
        foreach (var uid in allUsers)
        {
            try
            {
                await _audRepo.AddAsync(new NotifyAudience
                {
                    NotifyId = notifyId,
                    UserId = uid,
                    DeliveryStatus = 0,
                    CreatedAt = DateTime.Now
                }, ct);
            }
            catch (Exception ex) { Console.WriteLine($"[Notify] AddAudience failed for userId={uid}: {ex.Message}"); }
            await _queue.PublishAsync("notify.dispatch", new { notifyId, userId = uid }, ct);
        }

        msg.Status = 3; // Published
        await _msgRepo.UpdateAsync(msg, ct);
    }

    public async Task<PagedResult<NotifyListItemDto>> GetPagedAsync(PageRequest request, CancellationToken ct = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        var q = _msgRepo.Query();
        var total = q.LongCount();
        var items = q.OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new NotifyListItemDto { Id = x.Id, Title = x.Title, PublishedAt = x.PublishedAt, Status = x.Status })
            .ToList();
        return await Task.FromResult(new PagedResult<NotifyListItemDto> { Total = total, Page = page, PageSize = size, Items = items });
    }

    public async Task<List<MyNotifyListItemDto>> GetMyListAsync(long userId, CancellationToken ct = default)
    {
        // 避免跨上下文联接导致的空结果，先取出我的接收记录再回表取消息
        var myAudiences = _audRepo.Query().Where(x => x.UserId == userId).Take(500).ToList();
        if (myAudiences.Count == 0) return new List<MyNotifyListItemDto>();
        var ids = myAudiences.Select(x => x.NotifyId).Distinct().ToList();
        var msgs = _msgRepo.Query().Where(x => ids.Contains(x.Id)).ToList();
        var readSet = new HashSet<long>(myAudiences.Where(x => x.ReadAt != null).Select(x => x.NotifyId));
        var list = msgs
            .Select(m => new MyNotifyListItemDto
            {
                Id = m.Id,
                Title = m.Title,
                PublishedAt = m.PublishedAt,
                IsRead = readSet.Contains(m.Id)
            })
            .OrderByDescending(x => x.PublishedAt ?? DateTime.MinValue)
            .Take(200)
            .ToList();
        return await Task.FromResult(list);
    }

    public async Task<MyNotifyDetailDto?> GetMyDetailAsync(long userId, long notifyId, CancellationToken ct = default)
    {
        var a = _audRepo.Query().FirstOrDefault(x => x.UserId == userId && x.NotifyId == notifyId);
        var m = _msgRepo.Query().FirstOrDefault(x => x.Id == notifyId);
        if (a == null || m == null) return null;
        return await Task.FromResult(new MyNotifyDetailDto
        {
            Id = m.Id,
            Title = m.Title,
            ContentType = m.ContentType,
            ContentText = m.ContentText,
            ContentHtml = m.ContentHtml,
            PublishedAt = m.PublishedAt,
            IsRead = a.ReadAt != null
        });
    }

    public async Task MarkReadAsync(long userId, long notifyId, CancellationToken ct = default)
    {
        var a = _audRepo.Query().FirstOrDefault(x => x.UserId == userId && x.NotifyId == notifyId);
        if (a == null) return;
        if (a.ReadAt == null)
        {
            a.ReadAt = DateTime.Now;
            await _audRepo.UpdateAsync(a, ct);
        }
        var msg = _msgRepo.Query().FirstOrDefault(x => x.Id == notifyId);
        if (msg != null)
        {
            msg.ReadCount = _audRepo.Query().Count(x => x.NotifyId == notifyId && x.ReadAt != null);
            await _msgRepo.UpdateAsync(msg, ct);
        }
    }

    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default)
    {
        var n = _audRepo.Query().Count(x => x.UserId == userId && x.ReadAt == null);
        return await Task.FromResult(n);
    }

    public async Task<NotifyStatsDto> GetStatsAsync(long notifyId, CancellationToken ct = default)
    {
        // 总接收人
        var totalRecipients = _audRepo.Query().Count(x => x.NotifyId == notifyId);
        // 已送达（已推送）
        var delivered = _audRepo.Query().Where(x => x.NotifyId == notifyId && x.DeliveredAt != null).ToList();
        var deliveredCount = delivered.Count;
        // 已读人数与名单
        var readAud = _audRepo.Query().Where(x => x.NotifyId == notifyId && x.ReadAt != null).ToList();
        var readCount = readAud.Count;
        var readUsers = readAud
            .Select(x => new UserBriefDto
            {
                Id = x.UserId,
                Name = string.IsNullOrWhiteSpace(x.UserName)
                    ? (_userRepo.Query().Where(u => u.Id == x.UserId).Select(u => u.DisplayName).FirstOrDefault() ?? x.UserId.ToString())
                    : x.UserName!
            })
            .ToList();
        // 名单：已送达
        var deliveredUsers = delivered
            .Select(x => new UserBriefDto
            {
                Id = x.UserId,
                Name = string.IsNullOrWhiteSpace(x.UserName)
                    ? (_userRepo.Query().Where(u => u.Id == x.UserId).Select(u => u.DisplayName).FirstOrDefault() ?? x.UserId.ToString())
                    : x.UserName!
            })
            .ToList();
        // 名单：未读取（已送达但未读）
        var unreadIds = _audRepo.Query()
            .Where(x => x.NotifyId == notifyId && x.DeliveredAt != null && x.ReadAt == null)
            .Select(x => x.UserId)
            .ToList();
        var unreadUsers = _userRepo.Query()
            .Where(u => unreadIds.Contains(u.Id))
            .Select(u => new UserBriefDto { Id = u.Id, Name = u.DisplayName })
            .ToList();

        // 回填消息表中的 TotalRecipients 与 ReadCount（若变更）
        try
        {
            var msg = _msgRepo.Query().FirstOrDefault(x => x.Id == notifyId);
            if (msg != null && (msg.TotalRecipients != totalRecipients || msg.ReadCount != readCount))
            {
                msg.TotalRecipients = totalRecipients;
                msg.ReadCount = readCount;
                await _msgRepo.UpdateAsync(msg, ct);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Notify] Stats backfill failed for notifyId={notifyId}: {ex.Message}"); }

        return await Task.FromResult(new NotifyStatsDto
        {
            Id = notifyId,
            TotalRecipients = totalRecipients,
            DeliveredCount = deliveredCount,
            ReadCount = readCount,
            DeliveredUsers = deliveredUsers,
            UnreadUsers = unreadUsers,
            ReadUsers = readUsers
        });
    }

        // 兼容接口新增：摘要、详情、软删除（旧实现不再使用，提供最小实现以通过编译）
        public async Task<NotifyStatsSummaryDto> GetStatsSummaryAsync(long notifyId, CancellationToken ct = default)
        {
            var s = await GetStatsAsync(notifyId, ct);
            return new NotifyStatsSummaryDto
            {
                Id = notifyId,
                TotalRecipients = s.TotalRecipients,
                DeliveredCount = s.DeliveredCount,
                ReadCount = s.ReadCount,
                TopDeliveredUsers = s.DeliveredUsers.Take(10).ToList(),
                TopUnreadUsers = s.UnreadUsers.Take(10).ToList(),
                DetailUrl = $"/api/v1/notifications/{notifyId}/detail"
            };
        }

        public Task<PublishedNotifyDetailDto?> GetPublishedDetailAsync(long notifyId, string? listType = "unread", string? keyword = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            // 旧版本不支持已发布详情视图，返回 null（当前程序已切换到 V2，不会调用到这里）
            return Task.FromResult<PublishedNotifyDetailDto?>(null);
        }

        public Task SoftDeleteAsync(long notifyId, CancellationToken ct = default)
        {
            // 旧实现不支持软删除，保持空操作
            return Task.CompletedTask;
        }

}


