// 文件功能说明：
// 消息通知应用服务实现，封装消息的分页查询、详情获取、已读标记等业务逻辑。

using Ganss.Xss;
using Ginkgo.Domain;
using Ginkgo.Domain.Messages;
using Ginkgo.Domain.Users;
using Ginkgo.Realtime;
using Ginkgo.Shared;
using SqlSugar;

namespace Ginkgo.Application.Messages;

/// <summary>
/// 消息通知应用服务。
/// </summary>
public sealed class MessageAppService : IMessageAppService
{
    private readonly IRepository<Message> _repo;
    private readonly IRepository<MessageAttachment> _attachRepo;
    private readonly IRepository<MessageLink> _linkRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<UserRole> _userRoleRepo;
    private readonly IRepository<UserDepartment> _userDeptRepo;
    private readonly IRepository<Ginkgo.Domain.Files.SysFile> _fileRepo;
    private readonly IQueuePublisher _queue;

    public MessageAppService(
        IRepository<Message> repo,
        IRepository<MessageAttachment> attachRepo,
        IRepository<MessageLink> linkRepo,
        IRepository<User> userRepo,
        IRepository<UserRole> userRoleRepo,
        IRepository<UserDepartment> userDeptRepo,
        IRepository<Ginkgo.Domain.Files.SysFile> fileRepo,
        IQueuePublisher queue)
    {
        _repo = repo;
        _attachRepo = attachRepo;
        _linkRepo = linkRepo;
        _userRepo = userRepo;
        _userRoleRepo = userRoleRepo;
        _userDeptRepo = userDeptRepo;
        _fileRepo = fileRepo;
        _queue = queue;
    }

    /// <inheritdoc />
    public async Task<PagedResult<MessageListItemDto>> GetPagedListAsync(
        long userId, int page, int pageSize, bool? isRead = null, string? deliveryRole = null, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        var query = _repo.Query()
            .Where(m => m.UserId == userId && !m.IsDeleted);

        if (isRead.HasValue)
        {
            query = query.Where(m => m.IsRead == isRead.Value);
        }

        if (!string.IsNullOrEmpty(deliveryRole))
        {
            query = query.Where(m => m.DeliveryRole == deliveryRole);
        }

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = list.Select(m => new MessageListItemDto
        {
            Id = m.Id,
            Title = m.Title,
            Summary = m.Summary,
            Type = m.Type,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt,
            DeliveryRole = m.DeliveryRole
        }).ToList();

        return new PagedResult<MessageListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<MessageDetailDto> GetDetailAsync(long id, long userId, string? platform = null, CancellationToken ct = default)
    {
        var message = await _repo.GetByIdAsync(id, ct);

        if (message == null || message.IsDeleted)
            throw new KeyNotFoundException("消息不存在");

        if (message.UserId != userId)
            throw new UnauthorizedAccessException("无权访问该消息");

        // 查询关联附件（按创建时间排序）
        var attachments = await _attachRepo.Query()
            .Where(a => a.MessageId == message.Id && !a.IsDeleted)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        // 查询关联链接，按 platform 过滤
        var linkQuery = _linkRepo.Query()
            .Where(l => l.MessageId == message.Id && !l.IsDeleted);

        if (!string.IsNullOrEmpty(platform))
        {
            linkQuery = linkQuery.Where(l => l.Platform == platform);
        }

        var links = await linkQuery.ToListAsync();

        // 从文件表补齐附件的文件 URL
        var fileIds = attachments.Select(a => a.FileId).Distinct().ToList();
        var fileUrlMap = new Dictionary<long, string?>();
        if (fileIds.Count > 0)
        {
            var fileInfos = _fileRepo.Query()
                .Where(f => fileIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Url })
                .ToList();
            foreach (var fi in fileInfos) fileUrlMap[fi.Id] = fi.Url;
        }

        return new MessageDetailDto
        {
            Id = message.Id,
            Title = message.Title,
            Summary = message.Summary,
            Type = message.Type,
            IsRead = message.IsRead,
            CreatedAt = message.CreatedAt,
            Content = message.Content,
            ReadAt = message.ReadAt,
            DeliveryRole = message.DeliveryRole,
            Attachments = attachments.Select(a => new MessageAttachmentDto
            {
                Id = a.Id,
                FileId = a.FileId,
                FileName = a.FileName,
                FileSize = a.FileSize,
                AttachmentType = a.AttachmentType,
                FileUrl = fileUrlMap.TryGetValue(a.FileId, out var fUrl) ? fUrl : null
            }).ToList(),
            Links = links.Select(l => new MessageLinkDto
            {
                Id = l.Id,
                Title = l.Title,
                Platform = l.Platform,
                Url = l.Url
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(long id, long userId, CancellationToken ct = default)
    {
        var message = await _repo.GetByIdAsync(id, ct);

        if (message == null || message.IsDeleted)
            throw new KeyNotFoundException("消息不存在");

        if (message.UserId != userId)
            throw new UnauthorizedAccessException("无权访问该消息");

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.Now;
            await _repo.UpdateAsync(message, ct);
        }
    }

    /// <inheritdoc />
    public async Task MarkAllAsReadAsync(long userId, CancellationToken ct = default)
    {
        var unreadMessages = await _repo.Query()
            .Where(m => m.UserId == userId && !m.IsRead && !m.IsDeleted)
            .ToListAsync();

        if (unreadMessages.Count == 0) return;

        var now = DateTime.Now;
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = now;
        }

        await _repo.UpdateRangeAsync(unreadMessages, ct);
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default)
    {
        return await _repo.Query()
            .Where(m => m.UserId == userId && !m.IsRead && !m.IsDeleted)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task CreateAsync(CreateMessageInput input, CancellationToken ct = default)
    {
        // 1. 校验接收对象
        var (isValid, error) = RecipientGroupValidator.Validate(input);
        if (!isValid) throw new InvalidOperationException(error!);

        // 2. 解析主送接收人 ID 列表
        var primaryUserIds = await ResolveRecipientIds(input.Primary, ct);

        // 3. 解析知会接收人 ID 列表（如有）
        var ccUserIds = input.Cc != null
            ? await ResolveRecipientIds(input.Cc, ct)
            : new List<long>();

        // 4. 为主送接收人批量创建 Message + Attachments + Links
        var primaryMessages = await CreateMessagesForGroup(input, primaryUserIds, "primary", ct);

        // 5. 为知会接收人批量创建 Message + Attachments + Links
        List<Message> ccMessages = new();
        if (ccUserIds.Count > 0)
            ccMessages = await CreateMessagesForGroup(input, ccUserIds, "cc", ct);

        // 6. 推送实时通知到队列（每个用户一条）
        foreach (var msg in primaryMessages.Concat(ccMessages))
        {
            await _queue.PublishAsync("notify.dispatch", new { notifyId = msg.Id, userId = msg.UserId }, ct);
        }
    }

    /// <summary>
    /// 根据接收对象组的 Mode 解析实际用户 ID 列表。
    /// </summary>
    private async Task<List<long>> ResolveRecipientIds(RecipientGroupInput group, CancellationToken ct)
    {
        return group.Mode switch
        {
            "all" => await _userRepo.Query()
                .Where(u => u.Enabled && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(),
            "users" => group.Ids!,
            "roles" => await _userRoleRepo.Query()
                .Where(ur => group.Ids!.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(),
            "departments" => await _userDeptRepo.Query()
                .Where(ud => group.Ids!.Contains(ud.DepartmentId))
                .Select(ud => ud.UserId)
                .Distinct()
                .ToListAsync(),
            _ => throw new InvalidOperationException("无效的接收方式")
        };
    }

    /// <summary>
    /// 为一组接收人批量创建消息及其附件和链接。
    /// </summary>
    /// <summary>
    /// 共享的 HTML 净化器实例，移除脚本、事件处理器等危险内容，防止存储型 XSS。
    /// </summary>
    private static readonly HtmlSanitizer _htmlSanitizer = new();

    private async Task<List<Message>> CreateMessagesForGroup(
        CreateMessageInput input, List<long> userIds, string deliveryRole, CancellationToken ct)
    {
        // 净化 HTML 正文，防止存储型 XSS
        var sanitizedContent = string.IsNullOrEmpty(input.Content)
            ? input.Content
            : _htmlSanitizer.Sanitize(input.Content);

        var messages = userIds.Select(uid => new Message
        {
            UserId = uid,
            Title = input.Title,
            Summary = input.Summary,
            Content = sanitizedContent,
            Type = input.Type,
            DeliveryRole = deliveryRole,
            IsRead = false
        }).ToList();

        await _repo.AddRangeAsync(messages, ct);

        // 为每条消息创建附件
        if (input.Attachments?.Count > 0)
        {
            var attachments = messages.SelectMany(msg =>
                input.Attachments.Select(a => new MessageAttachment
                {
                    MessageId = msg.Id,
                    FileId = a.FileId,
                    FileName = a.FileName,
                    FileSize = a.FileSize,
                    AttachmentType = a.AttachmentType
                }));
            await _attachRepo.AddRangeAsync(attachments, ct);
        }

        // 为每条消息创建链接
        if (input.Links?.Count > 0)
        {
            var links = messages.SelectMany(msg =>
                input.Links.Select(l => new MessageLink
                {
                    MessageId = msg.Id,
                    Title = l.Title,
                    Platform = l.Platform,
                    Url = l.Url
                }));
            await _linkRepo.AddRangeAsync(links, ct);
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminMessageListItemDto>> GetAdminPagedListAsync(
        int page, int pageSize, string? title = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        var baseQuery = BuildAdminListQuery(title, startDate, endDate);

        // PostgreSQL 对 GROUP BY 聚合更严格，且 SqlSugar 链式 GroupBy 会污染原查询；统一内存分组
        var allMessages = await baseQuery.ToListAsync();
        var groups = allMessages
            .GroupBy(m => new { m.Title, Date = m.CreatedAt.Date })
            .Select(g => new AdminMessageListItemDto
            {
                Title = g.Key.Title,
                CreatedAt = g.Min(m => m.CreatedAt),
                TotalRecipients = g.Count(),
                ReadCount = g.Count(m => m.IsRead),
                Status = "Published"
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var total = groups.Count;
        var items = groups.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AdminMessageListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    private ISugarQueryable<Message> BuildAdminListQuery(string? title, DateTime? startDate, DateTime? endDate)
    {
        var baseQuery = _repo.Query().Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(title))
            baseQuery = baseQuery.Where(m => m.Title.Contains(title));

        if (startDate.HasValue)
            baseQuery = baseQuery.Where(m => m.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            baseQuery = baseQuery.Where(m => m.CreatedAt <= endDate.Value);

        return baseQuery;
    }

    /// <inheritdoc />
    public async Task<AdminMessageStatsDto> GetAdminStatsAsync(string title, DateTime createdAt, CancellationToken ct = default)
    {
        // 查找同一批次的所有消息（同标题 + 同一天）
        var startOfDay = createdAt.Date;
        var endOfDay = startOfDay.AddDays(1);

        var messages = await _repo.Query()
            .Where(m => !m.IsDeleted && m.Title == title && m.CreatedAt >= startOfDay && m.CreatedAt < endOfDay)
            .ToListAsync();

        // 获取用户名
        var userIds = messages.Select(m => m.UserId).Distinct().ToList();
        var users = await _userRepo.Query()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => !string.IsNullOrEmpty(u.DisplayName) ? u.DisplayName : u.UserName);

        var readMessages = messages.Where(m => m.IsRead).ToList();
        var unreadMessages = messages.Where(m => !m.IsRead).ToList();

        return new AdminMessageStatsDto
        {
            TotalRecipients = messages.Count,
            DeliveredCount = messages.Count,
            ReadCount = readMessages.Count,
            DeliveredUsers = messages.Select(m => new AdminRecipientInfo
            {
                Id = m.UserId.ToString(),
                Name = userMap.GetValueOrDefault(m.UserId, m.UserId.ToString())
            }).ToList(),
            UnreadUsers = unreadMessages.Select(m => new AdminRecipientInfo
            {
                Id = m.UserId.ToString(),
                Name = userMap.GetValueOrDefault(m.UserId, m.UserId.ToString())
            }).ToList(),
            ReadUsers = readMessages.Select(m => new AdminRecipientInfo
            {
                Id = m.UserId.ToString(),
                Name = userMap.GetValueOrDefault(m.UserId, m.UserId.ToString())
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<AdminMessageDetailDto> GetAdminDetailAsync(string title, DateTime createdAt, CancellationToken ct = default)
    {
        // 查找同一批次的所有消息
        var startOfDay = createdAt.Date;
        var endOfDay = startOfDay.AddDays(1);

        var messages = await _repo.Query()
            .Where(m => !m.IsDeleted && m.Title == title && m.CreatedAt >= startOfDay && m.CreatedAt < endOfDay)
            .ToListAsync();

        if (messages.Count == 0)
            throw new KeyNotFoundException("消息批次不存在");

        // 取第一条消息获取正文（同批次内容相同）
        var first = messages.First();

        // 查询附件（取第一条消息关联的附件）
        var attachments = await _attachRepo.Query()
            .Where(a => a.MessageId == first.Id && !a.IsDeleted)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        // 查询链接（取第一条消息关联的链接）
        var links = await _linkRepo.Query()
            .Where(l => l.MessageId == first.Id && !l.IsDeleted)
            .ToListAsync();

        // 从文件表补齐附件的文件 URL
        var adminFileIds = attachments.Select(a => a.FileId).Distinct().ToList();
        var adminFileUrlMap = new Dictionary<long, string?>();
        if (adminFileIds.Count > 0)
        {
            var adminFileInfos = _fileRepo.Query()
                .Where(f => adminFileIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Url })
                .ToList();
            foreach (var fi in adminFileInfos) adminFileUrlMap[fi.Id] = fi.Url;
        }

        return new AdminMessageDetailDto
        {
            Title = first.Title,
            CreatedAt = first.CreatedAt,
            Content = first.Content,
            Summary = first.Summary,
            Type = first.Type,
            TotalRecipients = messages.Count,
            ReadCount = messages.Count(m => m.IsRead),
            Attachments = attachments.Select(a => new MessageAttachmentDto
            {
                Id = a.Id,
                FileId = a.FileId,
                FileName = a.FileName,
                FileSize = a.FileSize,
                AttachmentType = a.AttachmentType,
                FileUrl = adminFileUrlMap.TryGetValue(a.FileId, out var aUrl) ? aUrl : null
            }).ToList(),
            Links = links.Select(l => new MessageLinkDto
            {
                Id = l.Id,
                Title = l.Title,
                Platform = l.Platform,
                Url = l.Url
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(string title, DateTime createdAt, CancellationToken ct = default)
    {
        var startOfDay = createdAt.Date;
        var endOfDay = startOfDay.AddDays(1);

        var messages = await _repo.Query()
            .Where(m => !m.IsDeleted && m.Title == title && m.CreatedAt >= startOfDay && m.CreatedAt < endOfDay)
            .ToListAsync();

        if (messages.Count == 0) return;

        var now = DateTime.Now;
        foreach (var msg in messages)
        {
            msg.IsDeleted = true;
            msg.DeletedAt = now;
        }

        await _repo.UpdateRangeAsync(messages, ct);
    }
}
