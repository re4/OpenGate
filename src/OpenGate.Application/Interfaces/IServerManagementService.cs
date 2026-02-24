using OpenGate.Extensions.Abstractions;

namespace OpenGate.Application.Interfaces;

public interface IServerManagementService
{
    Task<ServerStatus?> GetStatusAsync(string orderId, string userId);
    Task<ProvisionResult> StartAsync(string orderId, string userId);
    Task<ProvisionResult> StopAsync(string orderId, string userId);
    Task<ProvisionResult> RestartAsync(string orderId, string userId);
    Task<ProvisionResult> ReinstallAsync(string orderId, string userId, ReinstallOptions options);
    Task<ProvisionResult> CreateBackupAsync(string orderId, string userId);
    Task<IEnumerable<BackupInfo>> ListBackupsAsync(string orderId, string userId);
    Task<ProvisionResult> RestoreBackupAsync(string orderId, string userId, string backupId);
}
