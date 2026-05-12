using SqlSugar;

namespace Ginkgo.Domain.Notifications;

[SugarTable("ginkgo_Notify_Message", TableDescription = "通知消息主表")]
public sealed class NotificationMessage : Ginkgo.Domain.Entity
{
    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public new long Id { get; set; }

    [SugarColumn(Length = 400, IsNullable = false)]
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// 标题-多语言 JSON
    /// </summary>
    [SugarColumn(ColumnDataType = "json", IsNullable = true)]
    public string? TitleI18n { get; private set; }

    [SugarColumn(IsNullable = false)]
    public byte ContentType { get; private set; } = 1; // 1-Text 2-HTML

    [SugarColumn(IsNullable = true)]
    public string? ContentText { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ContentHtml { get; private set; }

    [SugarColumn(IsNullable = false)]
    public bool IsImportant { get; private set; }

    [SugarColumn(IsNullable = false)]
    public byte Priority { get; private set; } = 1;

    [SugarColumn(IsNullable = true)]
    public long? SenderId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? SenderName { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ScheduledAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedAt { get; private set; }

    [SugarColumn(IsNullable = false)]
    public byte Status { get; private set; } = 0; // 0-草稿 1-已发布 2-取消

    [SugarColumn(IsNullable = false)]
    public int TotalRecipients { get; private set; } = 0;

    [SugarColumn(IsNullable = false)]
    public int ReadCount { get; private set; } = 0;

    [SugarColumn(IsNullable = false)]
    public int ClickCount { get; private set; } = 0;

    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    [SugarColumn(IsNullable = true)]
    public long? CreatedBy { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? UpdatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? UpdatedBy { get; private set; }

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; private set; } = false;

    // 行为
    public static NotificationMessage Create(string title, byte contentType, string? text, string? html, bool important, byte priority, long? senderId, string? senderName, DateTime? scheduledAt)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("标题不能为空", nameof(title));
        if (contentType != 1 && contentType != 2) throw new ArgumentException("内容类型错误", nameof(contentType));
        var m = new NotificationMessage
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            Title = title.Trim(),
            ContentType = contentType,
            ContentText = contentType == 1 ? text : null,
            ContentHtml = contentType == 2 ? html : null,
            IsImportant = important,
            Priority = priority,
            SenderId = senderId,
            SenderName = senderName,
            ScheduledAt = scheduledAt,
            CreatedAt = DateTime.Now,
            CreatedBy = senderId,
            Status = 0
        };
        return m;
    }

    public void Publish(DateTime utcNow)
    {
        if (Status == 2) throw new InvalidOperationException("已取消的消息无法发布");
        Status = 1;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        Status = 2;
        UpdatedAt = utcNow;
    }

    public void IncreaseTotalRecipients(int delta)
    {
        TotalRecipients = Math.Max(0, TotalRecipients + delta);
    }

    public void IncreaseReadCount(int delta = 1)
    {
        ReadCount = Math.Max(0, ReadCount + delta);
    }

    public void IncreaseClickCount(int delta = 1)
    {
        ClickCount = Math.Max(0, ClickCount + delta);
    }
}

