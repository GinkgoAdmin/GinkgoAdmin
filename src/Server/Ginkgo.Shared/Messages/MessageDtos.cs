// 文件功能说明：
// 定义消息通知模块的 DTO。

namespace Ginkgo.Application.Messages;

/// <summary>
/// 消息列表项输出。
/// </summary>
public sealed class MessageListItemDto
{
    /// <summary>
    /// 消息 Id（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 消息标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 消息摘要。
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 消息类型：system/task/notice。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读。
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>送达角色：primary（主送）或 cc（知会）。</summary>
    public string DeliveryRole { get; set; } = "primary";
}

/// <summary>
/// 消息详情输出（包含正文和阅读时间）。
/// </summary>
public sealed class MessageDetailDto
{
    /// <summary>
    /// 消息 Id（Snowflake ID）。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 消息标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 消息摘要。
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 消息类型：system/task/notice。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读。
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 消息正文。
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 阅读时间。
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>送达角色：primary（主送）或 cc（知会）。</summary>
    public string DeliveryRole { get; set; } = "primary";

    /// <summary>附件列表。</summary>
    public List<MessageAttachmentDto> Attachments { get; set; } = new();

    /// <summary>链接列表。</summary>
    public List<MessageLinkDto> Links { get; set; } = new();
}

/// <summary>
/// 消息附件输出。
/// </summary>
public sealed class MessageAttachmentDto
{
    /// <summary>附件 Id。</summary>
    public long Id { get; set; }

    /// <summary>关联文件 ID（SysFile）。</summary>
    public long FileId { get; set; }

    /// <summary>文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>附件类型: image / file。</summary>
    public string AttachmentType { get; set; } = "file";

    /// <summary>文件相对路径（来自 SysFile.Url），前端可通过 resolveResourcePath 拼接为完整可访问地址。</summary>
    public string? FileUrl { get; set; }
}

/// <summary>
/// 消息链接输出。
/// </summary>
public sealed class MessageLinkDto
{
    /// <summary>链接 Id。</summary>
    public long Id { get; set; }

    /// <summary>链接标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>目标平台: web / wpf / uniapp。</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>跳转 URL（含路径和参数）。</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 接收对象组输入，每组只能选择一种接收方式。
/// </summary>
public sealed class RecipientGroupInput
{
    /// <summary>
    /// 接收方式: all / users / roles / departments，四选一互斥。
    /// </summary>
    public string Mode { get; set; } = "all";

    /// <summary>
    /// 当 Mode 为 users/roles/departments 时，对应的 ID 列表。
    /// Mode 为 all 时忽略此字段。
    /// </summary>
    public List<long>? Ids { get; set; }
}

/// <summary>
/// 消息创建请求体，支持主送 + 知会两组接收对象。
/// </summary>
public sealed class CreateMessageInput
{
    /// <summary>
    /// 消息标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 消息摘要。
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 消息正文。
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 消息类型，默认 system。
    /// </summary>
    public string Type { get; set; } = "system";

    /// <summary>
    /// 主送接收对象（必填）。
    /// </summary>
    public RecipientGroupInput Primary { get; set; } = new();

    /// <summary>
    /// 知会接收对象（可选，为 null 表示不设知会）。
    /// </summary>
    public RecipientGroupInput? Cc { get; set; }

    /// <summary>
    /// 附件列表（可选）。
    /// </summary>
    public List<CreateMessageAttachmentInput>? Attachments { get; set; }

    /// <summary>
    /// 链接列表（可选）。
    /// </summary>
    public List<CreateMessageLinkInput>? Links { get; set; }
}

/// <summary>
/// 消息附件创建输入。
/// </summary>
public sealed class CreateMessageAttachmentInput
{
    /// <summary>
    /// 关联文件 ID（SysFile）。
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// 文件名。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）。
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 附件类型: image / file。
    /// </summary>
    public string AttachmentType { get; set; } = "file";
}

/// <summary>
/// 消息链接创建输入。
/// </summary>
public sealed class CreateMessageLinkInput
{
    /// <summary>
    /// 链接标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 目标平台: web / wpf / uniapp。
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// 跳转 URL（含路径和参数）。
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 管理端消息列表项（按发送批次分组）。
/// </summary>
public sealed class AdminMessageListItemDto
{
    /// <summary>消息标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>发送时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>接收人总数。</summary>
    public int TotalRecipients { get; set; }

    /// <summary>已读人数。</summary>
    public int ReadCount { get; set; }

    /// <summary>状态：已发布。</summary>
    public string Status { get; set; } = "Published";
}

/// <summary>
/// 管理端消息详情输出（含正文、附件及链接，不校验用户归属）。
/// </summary>
public sealed class AdminMessageDetailDto
{
    /// <summary>消息标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>发送时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>消息正文。</summary>
    public string? Content { get; set; }

    /// <summary>消息摘要。</summary>
    public string? Summary { get; set; }

    /// <summary>消息类型。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>接收人总数。</summary>
    public int TotalRecipients { get; set; }

    /// <summary>已读人数。</summary>
    public int ReadCount { get; set; }

    /// <summary>附件列表。</summary>
    public List<MessageAttachmentDto> Attachments { get; set; } = new();

    /// <summary>链接列表。</summary>
    public List<MessageLinkDto> Links { get; set; } = new();
}

/// <summary>
/// 管理端消息投递统计。
/// </summary>
public sealed class AdminMessageStatsDto
{
    /// <summary>接收人总数。</summary>
    public int TotalRecipients { get; set; }

    /// <summary>已送达数（等于总数，因为消息创建即送达）。</summary>
    public int DeliveredCount { get; set; }

    /// <summary>已读数。</summary>
    public int ReadCount { get; set; }

    /// <summary>已送达用户列表。</summary>
    public List<AdminRecipientInfo> DeliveredUsers { get; set; } = new();

    /// <summary>未读用户列表。</summary>
    public List<AdminRecipientInfo> UnreadUsers { get; set; } = new();

    /// <summary>已读用户列表。</summary>
    public List<AdminRecipientInfo> ReadUsers { get; set; } = new();
}

/// <summary>
/// 接收人信息。
/// </summary>
public sealed class AdminRecipientInfo
{
    /// <summary>用户 Id。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>用户名。</summary>
    public string Name { get; set; } = string.Empty;
}
