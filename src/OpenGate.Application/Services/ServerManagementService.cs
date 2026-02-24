using Microsoft.Extensions.DependencyInjection;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Application.Services;

public class ServerManagementService(
    IOrderRepository orderRepository,
    IExtensionConfigRepository extensionConfigRepository,
    IServiceProvider serviceProvider) : IServerManagementService
{
    public async Task<ServerStatus?> GetStatusAsync(string orderId, string userId)
    {
        var (provisioner, externalId) = await ResolveAsync(orderId, userId);
        if (provisioner == null || externalId == null) return null;
        return await provisioner.GetServerStatusAsync(externalId);
    }

    public async Task<ProvisionResult> StartAsync(string orderId, string userId)
    {
        return await ExecuteAsync(orderId, userId, (p, id) => p.StartServerAsync(id));
    }

    public async Task<ProvisionResult> StopAsync(string orderId, string userId)
    {
        return await ExecuteAsync(orderId, userId, (p, id) => p.StopServerAsync(id));
    }

    public async Task<ProvisionResult> RestartAsync(string orderId, string userId)
    {
        return await ExecuteAsync(orderId, userId, (p, id) => p.RestartServerAsync(id));
    }

    public async Task<ProvisionResult> ReinstallAsync(string orderId, string userId, ReinstallOptions options)
    {
        return await ExecuteAsync(orderId, userId, (p, id) => p.ReinstallServerAsync(id, options));
    }

    public async Task<ProvisionResult> CreateBackupAsync(string orderId, string userId)
    {
        return await ExecuteAsync(orderId, userId, (p, id) => p.CreateBackupAsync(id));
    }

    public async Task<IEnumerable<BackupInfo>> ListBackupsAsync(string orderId, string userId)
    {
        var (provisioner, externalId) = await ResolveAsync(orderId, userId);
        if (provisioner == null || externalId == null) return [];
        return await provisioner.ListBackupsAsync(externalId);
    }

    public async Task<ProvisionResult> RestoreBackupAsync(string orderId, string userId, string backupId)
    {
        return await ExecuteAsync(orderId, userId, (p, id) => p.RestoreBackupAsync(id, backupId));
    }

    private async Task<ProvisionResult> ExecuteAsync(
        string orderId, string userId,
        Func<IServerProvisioner, string, Task<ProvisionResult>> action)
    {
        var (provisioner, externalId) = await ResolveAsync(orderId, userId);
        if (provisioner == null)
            return new ProvisionResult { Success = false, ErrorMessage = "No server provisioner configured." };
        if (externalId == null)
            return new ProvisionResult { Success = false, ErrorMessage = "This order has no provisioned server." };

        return await action(provisioner, externalId);
    }

    private async Task<(IServerProvisioner? Provisioner, string? ExternalId)> ResolveAsync(string orderId, string userId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order == null || order.UserId != userId)
            return (null, null);

        if (string.IsNullOrEmpty(order.ProvisioningId) || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Terminated)
            return (null, null);

        var configs = await extensionConfigRepository.GetByTypeAsync(ExtensionType.ServerProvisioner);
        var enabledConfig = configs.FirstOrDefault(c => c.IsEnabled);
        if (enabledConfig == null)
            return (null, null);

        var provisioners = serviceProvider.GetServices<IServerProvisioner>();
        var provisioner = provisioners.FirstOrDefault(p =>
            p.Name.Equals(enabledConfig.Name, StringComparison.OrdinalIgnoreCase));

        if (provisioner == null)
            return (null, null);

        await provisioner.InitializeAsync(enabledConfig.Settings);
        return (provisioner, order.ProvisioningId);
    }
}
