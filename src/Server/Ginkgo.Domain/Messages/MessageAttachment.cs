using SqlSugar;

namespace Ginkgo.Domain.Messages;

/// <summary>
/// 消息附件实体，关联消息与文件（图片或文件附件）。
/// </summary>
[SugarTable("ginkgo_Sys_MessageAttachment")]
[SugarIndex("IX_MsgAttachment_MessageId", nameof(MessageId), OrderByType.Asc)]
public sealed class MessageAttachment : AuditableEntity
{
    /// <summary>
    /// 关联消息ID。
    /// </summary>
    [SugarColumn(ColumnDescription = "关联消息ID")]
    public long MessageId { get; set; }

    /// <summary>
    /// 关联文件ID（SysFile）。
    /// </summary>
    [SugarColumn(ColumnDescription = "关联文件ID（SysFile）")]
    public long FileId { get; set; }

    /// <summary>
    /// 文件名。
    /// </summary>
    [SugarColumn(Length = 300, ColumnDescription = "文件名")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）。
    /// </summary>
    [SugarColumn(ColumnDescription = "文件大小（字节）")]
    public long FileSize { get; set; }

    /// <summary>
    /// 附件类型：image（图片）或 file（文件）。
    /// </summary>
    [SugarColumn(Length = 20, ColumnDescription = "附件类型: image/file")]
    public string AttachmentType { get; set; } = "file";
}
