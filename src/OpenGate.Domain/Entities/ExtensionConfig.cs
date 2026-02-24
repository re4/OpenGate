using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Entities;

public class ExtensionConfig : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ExtensionType Type { get; set; }
    public bool IsEnabled { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}
