using SqlSugar;

namespace Ginkgo.Domain.Notifications;

[SugarTable("ginkgo_Notify_AudienceSeed", TableDescription = "\u6536\u4ef6\u4eba\u79cd\u5b50\uff08\u89c4\u5219\u6216\u5916\u90e8\u76ee\u6807\uff09")]
public sealed class AudienceSeed : Ginkgo.Domain.Entity
{
    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public new long Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public long NotifyId { get; private set; }

    [SugarColumn(IsNullable = false)]
    public byte TargetType { get; private set; } // 0-User 1-Role 2-Dept 3-Expression

    [SugarColumn(Length = 256, IsNullable = false)]
    public string TargetValue { get; private set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    public static AudienceSeed Create(long notifyId, byte targetType, string targetValue)
    {
        if (string.IsNullOrWhiteSpace(targetValue)) throw new ArgumentException("TargetValue\u4e0d\u80fd\u4e3a\u7a7a", nameof(targetValue));
        return new AudienceSeed
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            NotifyId = notifyId,
            TargetType = targetType,
            TargetValue = targetValue.Trim(),
            CreatedAt = DateTime.Now
        };
    }
}

