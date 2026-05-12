using SqlSugar;

namespace Ginkgo.Domain.Notifications;

[SugarTable("ginkgo_Sys_NotifyMessage")]
public sealed class NotifyMessage : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public byte ContentType { get; set; } = 1; // 0:Text 1:Html 2:Markdown
    public string? ContentText { get; set; }
    public string? ContentHtml { get; set; }
    public bool IsImportant { get; set; }
    public byte Priority { get; set; } = 1;
    public long? SenderId { get; set; }
    public string? SenderName { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public byte Status { get; set; } = 0; // 0 Draft ...
    public int TotalRecipients { get; set; }
    public int ReadCount { get; set; }
    public int ClickCount { get; set; }
}

[SugarTable("ginkgo_Notify_AudienceSeed")]
public sealed class NotifyAudienceSeed : Entity
{
    public long NotifyId { get; set; }
    public byte TargetType { get; set; } // 1:User 2:Role 3:Dept 4:All
    public string TargetValue { get; set; } = string.Empty; // Guid or code
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[SugarTable("ginkgo_Sys_NotifyAudience")]
public sealed class NotifyAudience : Entity
{
    public long NotifyId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public long? DeptId { get; set; }
    public long? RoleId { get; set; }
    public byte DeliveryStatus { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ClickAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[SugarTable("ginkgo_Sys_NotifyAttachment")]
public sealed class NotifyAttachment : Entity
{
    public long NotifyId { get; set; }
    public long FileId { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public long? Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[SugarTable("ginkgo_Sys_NotifyDispatch")]
public sealed class NotifyDispatch : Entity
{
    public long NotifyId { get; set; }
    public long UserId { get; set; }
    public short Attempt { get; set; }
    public DateTime NextTryAt { get; set; } = DateTime.Now;
    public byte State { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}


