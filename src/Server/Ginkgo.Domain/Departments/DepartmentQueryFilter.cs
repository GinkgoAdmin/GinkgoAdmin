namespace Ginkgo.Domain.Departments;

public sealed class DepartmentQueryFilter
{
    public string? Keyword { get; init; }
    public bool? Enabled { get; init; }
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public long? ParentId { get; init; }
    public bool ParentDeep { get; init; } = false;
}

