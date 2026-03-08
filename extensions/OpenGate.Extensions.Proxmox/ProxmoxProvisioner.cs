using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.Proxmox;

public class ProxmoxProvisioner : IServerProvisioner
{
    private HttpClient _httpClient = null!;
    private string _apiUrl = string.Empty;
    private string _tokenId = string.Empty;
    private string _tokenSecret = string.Empty;
    private string _defaultNode = "pve";
    private string _defaultStorage = "local";
    private string _defaultTemplateId = "";
    private int _defaultMemory = 2048;
    private int _defaultCores = 1;
    private int _defaultDisk = 32;

    public string Name => "proxmox";
    public string DisplayName => "Proxmox VE";
    public string Version => "1.0.0";
    public string? Description => "Proxmox VE server provisioning integration";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _apiUrl = settings.GetValueOrDefault("ApiUrl", string.Empty).TrimEnd('/');
        _tokenId = settings.GetValueOrDefault("TokenId", string.Empty);
        _tokenSecret = settings.GetValueOrDefault("TokenSecret", string.Empty);
        _defaultNode = settings.GetValueOrDefault("DefaultNode", "pve");
        _defaultStorage = settings.GetValueOrDefault("DefaultStorage", "local");
        _defaultTemplateId = settings.GetValueOrDefault("DefaultTemplateId", "");
        _defaultMemory = int.TryParse(settings.GetValueOrDefault("DefaultMemory", "2048"), out var mem) ? mem : 2048;
        _defaultCores = int.TryParse(settings.GetValueOrDefault("DefaultCores", "1"), out var cores) ? cores : 1;
        _defaultDisk = int.TryParse(settings.GetValueOrDefault("DefaultDisk", "32"), out var disk) ? disk : 32;

        // TODO: Make SSL validation configurable via ProxmoxIgnoreSsl setting
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, sslPolicyErrors) =>
                sslPolicyErrors == System.Net.Security.SslPolicyErrors.None ||
                sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_apiUrl)
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("PVEAPIToken", $"{_tokenId}={_tokenSecret}");

        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["ApiUrl"] = "https://proxmox.example.com:8006/api2/json",
            ["TokenId"] = "user@pam!tokenname",
            ["TokenSecret"] = "",
            ["DefaultNode"] = "pve",
            ["DefaultStorage"] = "local",
            ["DefaultTemplateId"] = "",
            ["DefaultMemory"] = "2048",
            ["DefaultCores"] = "1",
            ["DefaultDisk"] = "32"
        };
    }

    public async Task<ProvisionResult> CreateServerAsync(ProvisionRequest request)
    {
        try
        {
            var node = GetOption(request.Options, "Node", _defaultNode);
            var templateId = GetOption(request.Options, "TemplateId", _defaultTemplateId);

            if (string.IsNullOrEmpty(templateId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "TemplateId is required. Set DefaultTemplateId in settings or pass TemplateId in Options."
                };
            }

            var newVmId = await GetNextVmIdAsync(node);
            if (newVmId == null)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to allocate a new VMID from Proxmox."
                };
            }

            var serverName = string.IsNullOrEmpty(request.ProductName)
                ? $"server-{request.OrderId}"
                : $"{request.ProductName}-{request.OrderId[..Math.Min(8, request.OrderId.Length)]}";

            var memory = int.TryParse(GetOption(request.Options, "Memory", ""), out var m) ? m : _defaultMemory;
            var cores = int.TryParse(GetOption(request.Options, "Cores", ""), out var c) ? c : _defaultCores;
            var storage = GetOption(request.Options, "Storage", _defaultStorage);
            var diskSize = int.TryParse(GetOption(request.Options, "Disk", ""), out var d) ? d : _defaultDisk;

            var cloneParams = new Dictionary<string, string>
            {
                ["newid"] = newVmId,
                ["name"] = serverName,
                ["target"] = node,
                ["full"] = "1",
                ["storage"] = storage,
                ["description"] = $"Order {request.OrderId} - {request.UserEmail}"
            };

            var cloneResponse = await PostFormAsync($"/nodes/{node}/qemu/{templateId}/clone", cloneParams);

            if (!cloneResponse.Success)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Proxmox clone error: {cloneResponse.Error}"
                };
            }

            var configParams = new Dictionary<string, string>
            {
                ["memory"] = memory.ToString(),
                ["cores"] = cores.ToString()
            };

            await PutFormAsync($"/nodes/{node}/qemu/{newVmId}/config", configParams);

            var startResponse = await PostFormAsync($"/nodes/{node}/qemu/{newVmId}/status/start", new());

            return new ProvisionResult
            {
                Success = true,
                ExternalId = $"{node}/{newVmId}",
                Metadata = new Dictionary<string, string>
                {
                    ["OrderId"] = request.OrderId,
                    ["ServerName"] = serverName,
                    ["Node"] = node,
                    ["VmId"] = newVmId,
                    ["StartedAfterClone"] = startResponse.Success.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<ProvisionResult> SuspendServerAsync(string externalId)
    {
        var (node, vmId) = ParseExternalId(externalId);
        return await PostActionAsync($"/nodes/{node}/qemu/{vmId}/status/suspend", "suspend");
    }

    public async Task<ProvisionResult> UnsuspendServerAsync(string externalId)
    {
        var (node, vmId) = ParseExternalId(externalId);
        return await PostActionAsync($"/nodes/{node}/qemu/{vmId}/status/resume", "resume");
    }

    public async Task<ProvisionResult> TerminateServerAsync(string externalId)
    {
        try
        {
            var (node, vmId) = ParseExternalId(externalId);

            await PostFormAsync($"/nodes/{node}/qemu/{vmId}/status/stop", new());

            await Task.Delay(2000);

            var response = await _httpClient.DeleteAsync($"/nodes/{node}/qemu/{vmId}?purge=1&destroy-unreferenced-disks=1");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Proxmox delete error ({(int)response.StatusCode}): {body}"
                };
            }

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<ServerStatus> GetServerStatusAsync(string externalId)
    {
        try
        {
            var (node, vmId) = ParseExternalId(externalId);
            var response = await _httpClient.GetAsync($"/nodes/{node}/qemu/{vmId}/status/current");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ServerStatus
                {
                    IsOnline = false,
                    StatusMessage = $"API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var data = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;

            var status = data.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "unknown" : "unknown";
            var qmpStatus = data.TryGetProperty("qmpstatus", out var qProp) ? qProp.GetString() : null;

            var resources = new Dictionary<string, object>();
            if (data.TryGetProperty("maxmem", out var memProp))
                resources["Memory (MB)"] = memProp.GetInt64() / 1024 / 1024;
            if (data.TryGetProperty("cpus", out var cpuProp))
                resources["CPUs"] = cpuProp.GetInt64();
            if (data.TryGetProperty("maxdisk", out var diskProp))
                resources["Disk (GB)"] = diskProp.GetInt64() / 1024 / 1024 / 1024;
            if (data.TryGetProperty("uptime", out var uptimeProp))
                resources["Uptime (s)"] = uptimeProp.GetInt64();
            if (data.TryGetProperty("name", out var nameProp))
                resources["Name"] = nameProp.GetString() ?? "";

            return new ServerStatus
            {
                IsOnline = status == "running",
                IsSuspended = qmpStatus == "paused",
                StatusMessage = qmpStatus ?? status,
                Resources = resources
            };
        }
        catch (Exception ex)
        {
            return new ServerStatus { IsOnline = false, StatusMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<ProvisionResult> StartServerAsync(string externalId)
    {
        var (node, vmId) = ParseExternalId(externalId);
        return await PostActionAsync($"/nodes/{node}/qemu/{vmId}/status/start", "start");
    }

    public async Task<ProvisionResult> StopServerAsync(string externalId)
    {
        var (node, vmId) = ParseExternalId(externalId);
        return await PostActionAsync($"/nodes/{node}/qemu/{vmId}/status/shutdown", "shutdown");
    }

    public async Task<ProvisionResult> RestartServerAsync(string externalId)
    {
        var (node, vmId) = ParseExternalId(externalId);
        return await PostActionAsync($"/nodes/{node}/qemu/{vmId}/status/reboot", "reboot");
    }

    public async Task<ProvisionResult> ReinstallServerAsync(string externalId, ReinstallOptions options)
    {
        try
        {
            var (node, vmId) = ParseExternalId(externalId);
            var templateId = options.OperatingSystemId;

            if (string.IsNullOrEmpty(templateId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "OperatingSystemId must be set to a Proxmox template VMID."
                };
            }

            await PostFormAsync($"/nodes/{node}/qemu/{vmId}/status/stop", new());
            await Task.Delay(3000);

            var configResponse = await _httpClient.GetAsync($"/nodes/{node}/qemu/{vmId}/config");
            var configContent = await configResponse.Content.ReadAsStringAsync();
            string vmName = "reinstalled";
            int memory = _defaultMemory;
            int cores = _defaultCores;

            if (configResponse.IsSuccessStatusCode)
            {
                using var configDoc = JsonDocument.Parse(configContent);
                var configData = configDoc.RootElement.TryGetProperty("data", out var cd) ? cd : configDoc.RootElement;
                if (configData.TryGetProperty("name", out var n)) vmName = n.GetString() ?? vmName;
                if (configData.TryGetProperty("memory", out var memP) && memP.TryGetInt32(out var memVal)) memory = memVal;
                if (configData.TryGetProperty("cores", out var coreP) && coreP.TryGetInt32(out var coreVal)) cores = coreVal;
            }

            var deleteResponse = await _httpClient.DeleteAsync($"/nodes/{node}/qemu/{vmId}?purge=1&destroy-unreferenced-disks=1");
            if (!deleteResponse.IsSuccessStatusCode)
            {
                var body = await deleteResponse.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to delete old VM: {body}"
                };
            }

            await Task.Delay(3000);

            var cloneParams = new Dictionary<string, string>
            {
                ["newid"] = vmId,
                ["name"] = vmName,
                ["target"] = node,
                ["full"] = "1"
            };

            var cloneResult = await PostFormAsync($"/nodes/{node}/qemu/{templateId}/clone", cloneParams);
            if (!cloneResult.Success)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Proxmox clone error during reinstall: {cloneResult.Error}"
                };
            }

            await PutFormAsync($"/nodes/{node}/qemu/{vmId}/config", new Dictionary<string, string>
            {
                ["memory"] = memory.ToString(),
                ["cores"] = cores.ToString()
            });

            await PostFormAsync($"/nodes/{node}/qemu/{vmId}/status/start", new());

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<ProvisionResult> CreateBackupAsync(string externalId)
    {
        try
        {
            var (node, vmId) = ParseExternalId(externalId);

            var backupParams = new Dictionary<string, string>
            {
                ["vmid"] = vmId,
                ["storage"] = _defaultStorage,
                ["mode"] = "snapshot",
                ["compress"] = "zstd"
            };

            var result = await PostFormAsync($"/nodes/{node}/vzdump", backupParams);

            return result.Success
                ? new ProvisionResult { Success = true, ExternalId = externalId }
                : new ProvisionResult { Success = false, ErrorMessage = $"Proxmox backup error: {result.Error}" };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<IEnumerable<BackupInfo>> ListBackupsAsync(string externalId)
    {
        try
        {
            var (node, vmId) = ParseExternalId(externalId);
            var response = await _httpClient.GetAsync($"/nodes/{node}/storage/{_defaultStorage}/content?content=backup&vmid={vmId}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(content);
            var data = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;

            if (data.ValueKind != JsonValueKind.Array)
                return [];

            var backups = new List<BackupInfo>();
            foreach (var item in data.EnumerateArray())
            {
                var volId = item.TryGetProperty("volid", out var vid) ? vid.GetString() ?? "" : "";
                var ctime = item.TryGetProperty("ctime", out var ct) && ct.TryGetInt64(out var epoch)
                    ? DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime
                    : DateTime.MinValue;
                var size = item.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var szVal) ? szVal : (long?)null;

                backups.Add(new BackupInfo
                {
                    Id = volId,
                    Name = volId.Split('/').LastOrDefault() ?? volId,
                    CreatedAt = ctime,
                    SizeBytes = size,
                    Status = "available"
                });
            }

            return backups.OrderByDescending(b => b.CreatedAt);
        }
        catch
        {
            return [];
        }
    }

    public async Task<ProvisionResult> RestoreBackupAsync(string externalId, string backupId)
    {
        try
        {
            var (node, vmId) = ParseExternalId(externalId);

            await PostFormAsync($"/nodes/{node}/qemu/{vmId}/status/stop", new());
            await Task.Delay(3000);

            var restoreParams = new Dictionary<string, string>
            {
                ["vmid"] = vmId,
                ["archive"] = backupId,
                ["force"] = "1",
                ["storage"] = _defaultStorage
            };

            var result = await PostFormAsync($"/nodes/{node}/qemu", restoreParams);

            if (!result.Success)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Proxmox restore error: {result.Error}"
                };
            }

            await PostFormAsync($"/nodes/{node}/qemu/{vmId}/status/start", new());

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    private static (string Node, string VmId) ParseExternalId(string externalId)
    {
        var parts = externalId.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("pve", parts[0]);
    }

    private static string GetOption(Dictionary<string, string> options, string key, string defaultValue)
    {
        return options.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : defaultValue;
    }

    private async Task<string?> GetNextVmIdAsync(string node)
    {
        try
        {
            var response = await _httpClient.GetAsync("/cluster/nextid");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                return data.ValueKind == JsonValueKind.Number
                    ? data.GetInt64().ToString()
                    : data.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ProvisionResult> PostActionAsync(string path, string actionName)
    {
        try
        {
            var result = await PostFormAsync(path, new());
            return result.Success
                ? new ProvisionResult { Success = true }
                : new ProvisionResult { Success = false, ErrorMessage = $"Proxmox {actionName} error {result.Error}" };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    private async Task<(bool Success, string? Error)> PostFormAsync(string path, Dictionary<string, string> parameters)
    {
        var content = new FormUrlEncodedContent(parameters);
        var response = await _httpClient.PostAsync(path, content);
        if (response.IsSuccessStatusCode)
            return (true, null);

        var body = await response.Content.ReadAsStringAsync();
        return (false, $"({(int)response.StatusCode}): {body}");
    }

    private async Task PutFormAsync(string path, Dictionary<string, string> parameters)
    {
        var content = new FormUrlEncodedContent(parameters);
        await _httpClient.PutAsync(path, content);
    }
}
