namespace Ginkgo.Plugin.Abstractions;

/// <summary>
/// 插件模块可用的消息通知服务接口。
/// 允许插件模块向指定用户发送系统消息，无需直接引用 Ginkgo.Domain。
/// </summary>
public interface IPluginMessageService
{
    /// <summary>
    /// 向指定用户发送一条消息。
    /// </summary>
    /// <param name="userId">接收用户的 Snowflake ID。</param>
    /// <param name="title">消息标题。</param>
    /// <param name="summary">消息摘要（可选）。</param>
    /// <param name="content">消息正文（可选）。</param>
    /// <param name="type">消息类型，如 system/task/notice，默认 system。</param>
    /// <param name="ct">取消令牌。</param>
    Task SendAsync(long userId, string title, string? summary = null, string? content = null, string type = "system", CancellationToken ct = default);

    /// <summary>
    /// 发送一条带有附件、链接和送达角色的消息。
    /// </summary>
    /// <param name="message">消息输入对象，包含完整的消息信息。</param>
    /// <param name="ct">取消令牌。</param>
    Task SendAsync(PluginMessageInput message, CancellationToken ct = default);

    /// <summary>
    /// 批量向多个用户发送消息。
    /// </summary>
    /// <param name="messages">消息列表。</param>
    /// <param name="ct">取消令牌。</param>
    Task SendBatchAsync(IEnumerable<PluginMessageInput> messages, CancellationToken ct = default);
}

/// <summary>
/// 插件消息输入项，支持附件、链接和送达角色。
/// </summary>
public sealed class PluginMessageInput
{
    /// <summary>接收用户的 Snowflake ID。</summary>
    public long UserId { get; set; }

    /// <summary>消息标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>消息摘要（可选）。</summary>
    public string? Summary { get; set; }

    /// <summary>消息正文（可选）。</summary>
    public string? Content { get; set; }

    /// <summary>消息类型，如 system/task/notice，默认 system。</summary>
    public string Type { get; set; } = "system";

    /// <summary>送达角色：primary（主送）或 cc（知会），默认 primary。</summary>
    public string DeliveryRole { get; set; } = "primary";

    /// <summary>附件列表（可选）。</summary>
    public List<PluginMessageAttachmentInput>? Attachments { get; set; }

    /// <summary>链接列表（可选）。</summary>
    public List<PluginMessageLinkInput>? Links { get; set; }
}

/// <summary>
/// 插件消息附件输入项。
/// </summary>
public sealed class PluginMessageAttachmentInput
{
    /// <summary>关联文件ID（SysFile）。</summary>
    public long FileId { get; set; }

    /// <summary>文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>附件类型：image（图片）或 file（文件），默认 file。</summary>
    public string AttachmentType { get; set; } = "file";
}

/// <summary>
/// 插件消息链接输入项。
/// </summary>
public sealed class PluginMessageLinkInput
{
    /// <summary>链接标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>目标平台：web/wpf/uniapp。</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>跳转URL（含路径和参数）。</summary>
    public string Url { get; set; } = string.Empty;
}
