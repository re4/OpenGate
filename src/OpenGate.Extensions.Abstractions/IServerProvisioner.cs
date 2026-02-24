namespace OpenGate.Extensions.Abstractions;

public interface IServerProvisioner : IOpenGateExtension
{
    Task<ProvisionResult> CreateServerAsync(ProvisionRequest request);
    Task<ProvisionResult> SuspendServerAsync(string externalId);
    Task<ProvisionResult> UnsuspendServerAsync(string externalId);
    Task<ProvisionResult> TerminateServerAsync(string externalId);
    Task<ServerStatus> GetServerStatusAsync(string externalId);

    Task<ProvisionResult> StartServerAsync(string externalId);
    Task<ProvisionResult> StopServerAsync(string externalId);
    Task<ProvisionResult> RestartServerAsync(string externalId);
    Task<ProvisionResult> ReinstallServerAsync(string externalId, ReinstallOptions options);
    Task<ProvisionResult> CreateBackupAsync(string externalId);
    Task<IEnumerable<BackupInfo>> ListBackupsAsync(string externalId);
    Task<ProvisionResult> RestoreBackupAsync(string externalId, string backupId);
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

public class ReinstallOptions
{
    public string OperatingSystemId { get; set; } = string.Empty;
    public string? Hostname { get; set; }
}

public class BackupInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? SizeBytes { get; set; }
    public string? Status { get; set; }
}
