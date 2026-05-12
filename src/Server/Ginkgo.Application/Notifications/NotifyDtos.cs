using System.Text.Json.Serialization;
using Ginkgo.Application.Notifications.Converters;


namespace Ginkgo.Application.Notifications;

public sealed class CreateNotifyInput
{
    public string Title { get; set; } = string.Empty;
    [JsonConverter(typeof(ContentTypeFlexibleConverter))]
    public byte ContentType { get; set; } = 1;
    public string? ContentText { get; set; }
    public string? ContentHtml { get; set; }
    public bool IsImportant { get; set; }
    public byte Priority { get; set; } = 1;
    public List<AudienceSeedInput> Audience { get; set; } = new();
    public List<AttachmentInput> Attachments { get; set; } = new();
}

	public sealed class UpdateNotifyInput
	{
	    public string Title { get; set; } = string.Empty;
	    [JsonConverter(typeof(ContentTypeFlexibleConverter))]
	    public byte ContentType { get; set; } = 1;
	    public string? ContentText { get; set; }
	    public string? ContentHtml { get; set; }
	    public bool IsImportant { get; set; }
	    public byte Priority { get; set; } = 1;
	    public List<AudienceSeedInput>? Audience { get; set; }
	    public List<AttachmentInput>? Attachments { get; set; }
	}


public sealed class AudienceSeedInput
{
    public byte TargetType { get; set; } // 1:User 2:Role 3:Dept 4:All
    public string TargetValue { get; set; } = string.Empty; // Guid/Code/All
}

public sealed class AttachmentInput
{
    public long FileId { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public long? Size { get; set; }
}

public sealed class UserBriefDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class NotifyStatsDto
{
    public long Id { get; set; }
    public int TotalRecipients { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public List<UserBriefDto> DeliveredUsers { get; set; } = new();
    public List<UserBriefDto> UnreadUsers { get; set; } = new();
    public List<UserBriefDto> ReadUsers { get; set; } = new();
}

public sealed class PublishNotifyInput
{
    public long NotifyId { get; set; }
}

public sealed class NotifyListItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public byte Status { get; set; }
}

public sealed class MyNotifyListItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public bool IsRead { get; set; }
}

public sealed class MyNotifyDetailDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public byte ContentType { get; set; }
    public string? ContentText { get; set; }
    public string? ContentHtml { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsRead { get; set; }
}




// 统计摘要（用于列表侧边栏/卡片，只返回前10个，避免撑爆UI）
public sealed class NotifyStatsSummaryDto
{
    public long Id { get; set; }
    public int TotalRecipients { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int UnreadCount => TotalRecipients - ReadCount;
    public List<UserBriefDto> TopDeliveredUsers { get; set; } = new();
    public List<UserBriefDto> TopUnreadUsers { get; set; } = new();
    public string? DetailUrl { get; set; } // 可用于前端跳转到完整详情
}

// 已发布通知详情（含可分页的名单检索）
public sealed class PublishedNotifyDetailDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public byte ContentType { get; set; }
    public string? ContentText { get; set; }
    public string? ContentHtml { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsDeleted { get; set; }

    // 附件
    public List<AttachmentDto> Attachments { get; set; } = new();

    // 汇总统计
    public int TotalRecipients { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int UnreadCount => TotalRecipients - ReadCount;

    // 名单分页（按需加载）
    public string ListType { get; set; } = "unread"; // delivered|read|unread
    public string? Keyword { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public List<UserBriefDto> Users { get; set; } = new();
}
