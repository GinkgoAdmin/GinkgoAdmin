namespace Ginkgo.Application.Notifications;

public sealed class AudienceSeedDto
{
    public byte TargetType { get; set; }
    public string TargetValue { get; set; } = string.Empty;
}

public sealed class AttachmentDto
{
    public long FileId { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public long? Size { get; set; }
    /// <summary>文件相对路径（来自 SysFile.Url），前端可通过 resolveResourcePath 拼接为完整可访问地址。</summary>
    public string? FileUrl { get; set; }
}

public sealed class CreateNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public byte ContentType { get; set; } = 1; // 1-Text 2-HTML
    public string? ContentText { get; set; }
    public string? ContentHtml { get; set; }
    public bool IsImportant { get; set; }
    public byte Priority { get; set; } = 1;
    public DateTime? ScheduledAt { get; set; }
    public List<AudienceSeedDto> Seeds { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public sealed class InboxItemDto
{
    public long NotifyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

