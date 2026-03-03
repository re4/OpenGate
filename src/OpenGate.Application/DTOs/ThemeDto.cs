namespace OpenGate.Application.DTOs;

public class ThemeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPreset { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class CreateThemeDto
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class UpdateThemeDto
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
}
