namespace Ginkgo.Domain.Permissions;

public sealed class Permission : Ginkgo.Domain.AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // API/Menu/Button
    public string? Resource { get; set; }
    public string? Method { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
}


