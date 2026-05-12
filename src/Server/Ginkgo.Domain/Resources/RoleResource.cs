namespace Ginkgo.Domain.Resources;

public sealed class RoleResource : Ginkgo.Domain.Entity
{
    public long RoleId { get; set; }
    public long ResourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long? CreatedBy { get; set; }
}


