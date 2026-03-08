using nClam;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class ClamAvService(IServiceScopeFactory scopeFactory, ILogger<ClamAvService> logger) : IClamAvService
{
    private ClamAvConfig? _config;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsEnabled => GetConfigSync()?.Enabled ?? false;

    public async Task<ScanResult> ScanAsync(Stream fileStream, CancellationToken ct = default)
    {
        var config = await GetConfigAsync();
        if (!config.Enabled)
            return ScanResult.Skip();

        try
        {
            var clam = new ClamClient(config.Host, config.Port);
            var result = await clam.SendAndScanFileAsync(fileStream, ct);

            return result.Result switch
            {
                ClamScanResults.Clean => ScanResult.Clean(),
                ClamScanResults.VirusDetected => ScanResult.Infected(
                    result.InfectedFiles?.FirstOrDefault()?.VirusName ?? "Unknown threat"),
                _ => ScanResult.Skip()
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClamAV scan failed");
            return ScanResult.Skip();
        }
    }

    private ClamAvConfig? GetConfigSync() => _config;

    private async Task<ClamAvConfig> GetConfigAsync()
    {
        if (_config != null) return _config;

        await _lock.WaitAsync();
        try
        {
            if (_config != null) return _config;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISettingRepository>();

            var enabled = (await repo.GetByKeyAsync("ClamAvEnabled"))?.Value ?? "false";
            var host = (await repo.GetByKeyAsync("ClamAvHost"))?.Value ?? "127.0.0.1";
            var portStr = (await repo.GetByKeyAsync("ClamAvPort"))?.Value ?? "3310";

            int.TryParse(portStr, out var port);
            if (port <= 0) port = 3310;

            _config = new ClamAvConfig
            {
                Enabled = string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase),
                Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim(),
                Port = port
            };
            return _config;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateCache() => _config = null;

    private class ClamAvConfig
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 3310;
    }
}
