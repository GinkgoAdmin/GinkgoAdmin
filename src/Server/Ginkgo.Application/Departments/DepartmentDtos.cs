using System.ComponentModel.DataAnnotations;

namespace Ginkgo.Application.Departments;

public sealed class DepartmentListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool Enabled { get; set; }
}

public sealed class CreateDepartmentInput
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(64)] public string? Code { get; set; }
    public long? ParentId { get; set; }
}

public sealed class UpdateDepartmentInput
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Order { get; set; }
}

public sealed class DepartmentTreeNodeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<DepartmentTreeNodeDto> Children { get; set; } = new();
}

public sealed class DepartmentDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public long? ParentId { get; set; }
    public bool Enabled { get; set; }
    public int Order { get; set; }
}

public sealed class DepartmentUserDto
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsManager { get; set; }
}




