using SqlSugar;

namespace Ginkgo.Domain.Notifications;

[SugarTable("ginkgo_Notify_Attachment", TableDescription = "\u901a\u77e5\u9644\u4ef6")]
public sealed class AttachmentVO : Ginkgo.Domain.Entity
{
    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public new long Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public long NotifyId { get; private set; }

    [SugarColumn(IsNullable = false)]
    public long FileId { get; private set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? Name { get; private set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? ContentType { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? Size { get; private set; }

    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    public static AttachmentVO Create(long notifyId, long fileId, string? name, string? contentType, long? size)
    {
        return new AttachmentVO
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            NotifyId = notifyId,
            FileId = fileId,
            Name = name,
            ContentType = contentType,
            Size = size,
            CreatedAt = DateTime.Now
        };
    }
}

