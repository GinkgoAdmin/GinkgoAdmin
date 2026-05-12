using Ginkgo.Domain;
using Ginkgo.Domain.Messages;
using Ginkgo.Plugin.Abstractions;

namespace Ginkgo.Application.Messages;

/// <summary>
/// IPluginMessageService 的实现，供插件模块发送系统消息。
/// </summary>
public sealed class PluginMessageService : IPluginMessageService
{
    private readonly IRepository<Message> _repo;
    private readonly IRepository<MessageAttachment> _attachRepo;
    private readonly IRepository<MessageLink> _linkRepo;

    public PluginMessageService(
        IRepository<Message> repo,
        IRepository<MessageAttachment> attachRepo,
        IRepository<MessageLink> linkRepo)
    {
        _repo = repo;
        _attachRepo = attachRepo;
        _linkRepo = linkRepo;
    }

    public async Task SendAsync(long userId, string title, string? summary = null, string? content = null, string type = "system", CancellationToken ct = default)
    {
        var msg = new Message
        {
            UserId = userId,
            Title = title,
            Summary = summary,
            Content = content,
            Type = type,
            IsRead = false,
            DeliveryRole = "primary"
        };
        await _repo.AddAsync(msg, ct);
    }

    public async Task SendAsync(PluginMessageInput input, CancellationToken ct = default)
    {
        var msg = new Message
        {
            UserId = input.UserId,
            Title = input.Title,
            Summary = input.Summary,
            Content = input.Content,
            Type = input.Type,
            DeliveryRole = input.DeliveryRole,
            IsRead = false
        };
        await _repo.AddAsync(msg, ct);

        if (input.Attachments?.Count > 0)
        {
            var attachments = input.Attachments.Select(a => new MessageAttachment
            {
                MessageId = msg.Id,
                FileId = a.FileId,
                FileName = a.FileName,
                FileSize = a.FileSize,
                AttachmentType = a.AttachmentType
            });
            await _attachRepo.AddRangeAsync(attachments, ct);
        }

        if (input.Links?.Count > 0)
        {
            var links = input.Links.Select(l => new MessageLink
            {
                MessageId = msg.Id,
                Title = l.Title,
                Platform = l.Platform,
                Url = l.Url
            });
            await _linkRepo.AddRangeAsync(links, ct);
        }
    }

    public async Task SendBatchAsync(IEnumerable<PluginMessageInput> messages, CancellationToken ct = default)
    {
        var entities = messages.Select(m => new Message
        {
            UserId = m.UserId,
            Title = m.Title,
            Summary = m.Summary,
            Content = m.Content,
            Type = m.Type,
            DeliveryRole = m.DeliveryRole,
            IsRead = false
        }).ToList();

        if (entities.Count > 0)
        {
            await _repo.AddRangeAsync(entities, ct);
        }
    }
}
