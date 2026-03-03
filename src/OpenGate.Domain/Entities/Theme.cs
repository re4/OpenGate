namespace OpenGate.Domain.Entities;

public class Theme : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPreset { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}
