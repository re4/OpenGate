namespace OpenGate.Extensions.Abstractions;

public interface IServerProvisioner : IOpenGateExtension
{
    Task<ProvisionResult> CreateServerAsync(ProvisionRequest request);
    Task<ProvisionResult> SuspendServerAsync(string externalId);
    Task<ProvisionResult> UnsuspendServerAsync(string externalId);
    Task<ProvisionResult> TerminateServerAsync(string externalId);
    Task<ServerStatus> GetServerStatusAsync(string externalId);
}

public class ProvisionRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ProvisionResult
{
    public bool Success { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ServerStatus
{
    public bool IsOnline { get; set; }
    public bool IsSuspended { get; set; }
    public string? StatusMessage { get; set; }
    public Dictionary<string, object> Resources { get; set; } = new();
}
