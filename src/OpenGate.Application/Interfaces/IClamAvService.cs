namespace OpenGate.Application.Interfaces;

public class ScanResult
{
    public bool IsClean { get; set; }
    public string? Threat { get; set; }
    public bool Skipped { get; set; }

    public static ScanResult Clean() => new() { IsClean = true };
    public static ScanResult Skip() => new() { IsClean = true, Skipped = true };
    public static ScanResult Infected(string threat) => new() { IsClean = false, Threat = threat };
}

public interface IClamAvService
{
    Task<ScanResult> ScanAsync(Stream fileStream, CancellationToken ct = default);
    bool IsEnabled { get; }
}
