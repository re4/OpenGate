namespace OpenGate.Extensions.Abstractions;

public interface IOpenGateExtension
{
    string Name { get; }
    string DisplayName { get; }
    string Version { get; }
    string? Description { get; }
    Task InitializeAsync(Dictionary<string, string> settings);
    Dictionary<string, string> GetDefaultSettings();
}
