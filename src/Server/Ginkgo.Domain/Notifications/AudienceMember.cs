using SqlSugar;

namespace Ginkgo.Domain.Notifications;

[SugarTable("ginkgo_Notify_Audience", TableDescription = "收件人明细（含快照与状态）")]
public sealed class AudienceMember : Ginkgo.Domain.Entity
{
    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public new long Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public long NotifyId { get; private set; }

    [SugarColumn(IsNullable = false)]
    public long UserId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? UserName { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? DeptId { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? RoleId { get; private set; }

    [SugarColumn(IsNullable = false)]
    public byte DeliveryStatus { get; private set; } = 0; // 0-待投递 1-成功 2-失败

    [SugarColumn(IsNullable = true)]
    public DateTime? DeliveredAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ReadAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ClickAt { get; private set; }

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? LastError { get; private set; }

    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    public static AudienceMember Create(long notifyId, long userId, string? userName = null, long? deptId = null, long? roleId = null)
    {
        return new AudienceMember
        {
            Id = Ginkgo.Domain.Utils.SnowflakeIdGenerator.NextId(),
            NotifyId = notifyId,
            UserId = userId,
            UserName = userName,
            DeptId = deptId,
            RoleId = roleId,
            CreatedAt = DateTime.Now
        };
    }

    public void MarkDelivered(DateTime utcNow) { DeliveryStatus = 1; DeliveredAt = utcNow; }
    public void MarkFailed(string error) { DeliveryStatus = 2; LastError = error; }
    public void MarkRead(DateTime utcNow) { ReadAt = utcNow; }
    public void MarkClicked(DateTime utcNow) { ClickAt = utcNow; }
}

