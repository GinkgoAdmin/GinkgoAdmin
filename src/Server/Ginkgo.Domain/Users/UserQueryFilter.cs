namespace Ginkgo.Domain.Users;

public sealed class UserQueryFilter
{
    public string? Keyword { get; init; }
    public bool? Enabled { get; init; }
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public long? DepartmentId { get; init; }
    public bool DepartmentDeep { get; init; } = false;
}

