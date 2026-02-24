namespace OpenGate.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
