using SqlSugar;

namespace Ginkgo.Domain.Messages;

/// <summary>
/// 消息链接实体，关联消息与跳转链接，支持多平台（web/wpf/uniapp）。
/// </summary>
[SugarTable("ginkgo_Sys_MessageLink")]
[SugarIndex("IX_MsgLink_MessageId", nameof(MessageId), OrderByType.Asc)]
public sealed class MessageLink : AuditableEntity
{
    /// <summary>
    /// 关联消息ID。
    /// </summary>
    [SugarColumn(ColumnDescription = "关联消息ID")]
    public long MessageId { get; set; }

    /// <summary>
    /// 链接标题。
    /// </summary>
    [SugarColumn(Length = 200, ColumnDescription = "链接标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 目标平台：web、wpf 或 uniapp。
    /// </summary>
    [SugarColumn(Length = 20, ColumnDescription = "目标平台: web/wpf/uniapp")]
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// 跳转URL（含路径和参数）。
    /// </summary>
    [SugarColumn(Length = 1000, ColumnDescription = "跳转URL（含路径和参数）")]
    public string Url { get; set; } = string.Empty;
}
