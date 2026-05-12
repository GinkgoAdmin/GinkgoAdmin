using SqlSugar;

namespace Ginkgo.Domain.Messages;

/// <summary>
/// 系统消息实体，用于存储发送给用户的通知消息。
/// </summary>
[SugarTable("ginkgo_Sys_Message")]
[SugarIndex("IX_Message_UserId_IsRead", $"{nameof(UserId)},{nameof(IsRead)}", OrderByType.Asc)]
[SugarIndex("IX_Message_UserId_CreatedAt", $"{nameof(UserId)},{nameof(CreatedAt)}", OrderByType.Desc)]
public sealed class Message : AuditableEntity
{
    /// <summary>
    /// 接收用户ID（Snowflake ID）。
    /// </summary>
    [SugarColumn(ColumnDescription = "接收用户Id")]
    public long UserId { get; set; }

    /// <summary>
    /// 消息标题。
    /// </summary>
    [SugarColumn(Length = 200, ColumnDescription = "消息标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 消息摘要。
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "消息摘要")]
    public string? Summary { get; set; }

    /// <summary>
    /// 消息正文。
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true, ColumnDescription = "消息正文")]
    public string? Content { get; set; }

    /// <summary>
    /// 消息类型：system/task/notice。
    /// </summary>
    [SugarColumn(Length = 50, ColumnDescription = "消息类型: system/task/notice")]
    public string Type { get; set; } = "system";

    /// <summary>
    /// 是否已读。
    /// </summary>
    [SugarColumn(ColumnDescription = "是否已读")]
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// 阅读时间。
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "阅读时间")]
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// 送达角色：primary（主送）或 cc（知会）。
    /// </summary>
    [SugarColumn(Length = 20, ColumnDescription = "送达角色: primary/cc")]
    public string DeliveryRole { get; set; } = "primary";
}
