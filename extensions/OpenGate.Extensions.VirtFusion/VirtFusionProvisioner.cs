using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.VirtFusion;

public class VirtFusionProvisioner : IServerProvisioner
{
    private readonly HttpClient _httpClient = new();
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private string _defaultOperatingSystemId = "1";
    private string _defaultHypervisorGroupId = "1";
    private string _defaultPackageId = "1";

    public string Name => "virtfusion";
    public string DisplayName => "VirtFusion";
    public string Version => "1.0.0";
    public string? Description => "VirtFusion virtualization platform provisioning integration";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        _apiUrl = settings.GetValueOrDefault("ApiUrl", string.Empty).TrimEnd('/');
        _apiToken = settings.GetValueOrDefault("ApiToken", string.Empty);
        _defaultOperatingSystemId = settings.GetValueOrDefault("DefaultOperatingSystemId", "1");
        _defaultHypervisorGroupId = settings.GetValueOrDefault("DefaultHypervisorGroupId", "1");
        _defaultPackageId = settings.GetValueOrDefault("DefaultPackageId", "1");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.BaseAddress = new Uri(_apiUrl);

        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["ApiUrl"] = "https://virtfusion.example.com/api/v1",
            ["ApiToken"] = "",
            ["DefaultOperatingSystemId"] = "1",
            ["DefaultHypervisorGroupId"] = "1",
            ["DefaultPackageId"] = "1"
        };
    }

    public async Task<ProvisionResult> CreateServerAsync(ProvisionRequest request)
    {
        try
        {
            var userId = request.Options.GetValueOrDefault("VirtFusionUserId");
            if (string.IsNullOrEmpty(userId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "VirtFusionUserId is required in Options. The user must exist in VirtFusion."
                };
            }

            var hypervisorGroupId = GetOptionOrDefault(request.Options, "HypervisorGroupId", _defaultHypervisorGroupId);
            var packageId = GetOptionOrDefault(request.Options, "PackageId", _defaultPackageId);
            var osId = GetOptionOrDefault(request.Options, "OperatingSystemId", _defaultOperatingSystemId);
            var ipv6 = request.Options.GetValueOrDefault("IPv6", "false");

            var serverName = string.IsNullOrEmpty(request.ProductName)
                ? $"server-{request.OrderId}"
                : $"{request.ProductName}-{request.OrderId[..Math.Min(8, request.OrderId.Length)]}";

            var hostname = request.Options.GetValueOrDefault("Hostname", $"{serverName}.vm");

            // Step 1: Create the server in VirtFusion
            var createPayload = new
            {
                userId = int.Parse(userId),
                hypervisorGroupId = int.Parse(hypervisorGroupId),
                packageId = int.Parse(packageId),
                name = serverName,
                hostname,
                ipv6 = bool.TryParse(ipv6, out var ipv6Enabled) && ipv6Enabled
            };

            var createJson = JsonSerializer.Serialize(createPayload);
            var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

            var createResponse = await _httpClient.PostAsync("/servers", createContent);
            var createResponseContent = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion create error ({(int)createResponse.StatusCode}): {createResponseContent}"
                };
            }

            using var createDoc = JsonDocument.Parse(createResponseContent);
            var serverId = ExtractServerId(createDoc.RootElement);

            if (string.IsNullOrEmpty(serverId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to extract server ID from VirtFusion create response."
                };
            }

            // Step 2: Build the server with the OS template
            var buildPayload = new
            {
                operatingSystemId = int.Parse(osId),
                name = serverName,
                hostname
            };

            var buildJson = JsonSerializer.Serialize(buildPayload);
            var buildContent = new StringContent(buildJson, Encoding.UTF8, "application/json");

            var buildResponse = await _httpClient.PostAsync($"/servers/{serverId}/build", buildContent);
            var buildResponseContent = await buildResponse.Content.ReadAsStringAsync();

            if (!buildResponse.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = true,
                    ExternalId = serverId,
                    ErrorMessage = $"Server created (ID: {serverId}) but build failed ({(int)buildResponse.StatusCode}): {buildResponseContent}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["OrderId"] = request.OrderId,
                        ["ServerName"] = serverName,
                        ["BuildStatus"] = "failed"
                    }
                };
            }

            return new ProvisionResult
            {
                Success = true,
                ExternalId = serverId,
                Metadata = new Dictionary<string, string>
                {
                    ["OrderId"] = request.OrderId,
                    ["ServerName"] = serverName,
                    ["BuildStatus"] = "queued"
                }
            };
        }
        catch (Exception ex)
        {
            return new ProvisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ProvisionResult> SuspendServerAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/servers/{externalId}/suspend", null);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion suspend error ({(int)response.StatusCode}): {content}"
                };
            }

            return new ProvisionResult
            {
                Success = true,
                ExternalId = externalId
            };
        }
        catch (Exception ex)
        {
            return new ProvisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ProvisionResult> UnsuspendServerAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/servers/{externalId}/unsuspend", null);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion unsuspend error ({(int)response.StatusCode}): {content}"
                };
            }

            return new ProvisionResult
            {
                Success = true,
                ExternalId = externalId
            };
        }
        catch (Exception ex)
        {
            return new ProvisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ProvisionResult> TerminateServerAsync(string externalId)
    {
        try
        {
            var delay = "0";
            var response = await _httpClient.DeleteAsync($"/servers/{externalId}?delay={delay}");

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion delete error ({(int)response.StatusCode}): {content}"
                };
            }

            return new ProvisionResult
            {
                Success = true,
                ExternalId = externalId
            };
        }
        catch (Exception ex)
        {
            return new ProvisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ServerStatus> GetServerStatusAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/servers/{externalId}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ServerStatus
                {
                    IsOnline = false,
                    IsSuspended = false,
                    StatusMessage = $"VirtFusion API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var data = root.TryGetProperty("data", out var dataProp) ? dataProp : root;

            var isSuspended = data.TryGetProperty("suspended", out var suspendedProp) && suspendedProp.GetBoolean();
            var state = data.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "unknown" : "unknown";
            var buildFailed = data.TryGetProperty("buildFailed", out var buildFailedProp) && buildFailedProp.GetBoolean();

            var resources = new Dictionary<string, object>();

            if (data.TryGetProperty("settings", out var settings) &&
                settings.TryGetProperty("resources", out var resourcesProp))
            {
                if (resourcesProp.TryGetProperty("memory", out var memProp))
                    resources["Memory"] = memProp.GetInt64();
                if (resourcesProp.TryGetProperty("storage", out var storageProp))
                    resources["Storage"] = storageProp.GetInt64();
                if (resourcesProp.TryGetProperty("cpuCores", out var cpuProp))
                    resources["CpuCores"] = cpuProp.GetInt64();
                if (resourcesProp.TryGetProperty("traffic", out var trafficProp))
                    resources["Traffic"] = trafficProp.GetInt64();
            }

            var statusMessage = state;
            if (buildFailed)
                statusMessage = "build_failed";

            return new ServerStatus
            {
                IsOnline = state is "active" or "running" or "online",
                IsSuspended = isSuspended,
                StatusMessage = statusMessage,
                Resources = resources
            };
        }
        catch (Exception ex)
        {
            return new ServerStatus
            {
                IsOnline = false,
                IsSuspended = false,
                StatusMessage = ex.Message
            };
        }
    }

    public async Task<ProvisionResult> StartServerAsync(string externalId)
    {
        return await SendPowerActionAsync(externalId, "boot");
    }

    public async Task<ProvisionResult> StopServerAsync(string externalId)
    {
        return await SendPowerActionAsync(externalId, "shutdown");
    }

    public async Task<ProvisionResult> RestartServerAsync(string externalId)
    {
        return await SendPowerActionAsync(externalId, "restart");
    }

    public async Task<ProvisionResult> ReinstallServerAsync(string externalId, ReinstallOptions options)
    {
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["operatingSystemId"] = int.Parse(options.OperatingSystemId)
            };
            if (!string.IsNullOrEmpty(options.Hostname))
                payload["hostname"] = options.Hostname;

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/servers/{externalId}/build", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion reinstall error ({(int)response.StatusCode}): {body}"
                };
            }

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ProvisionResult> CreateBackupAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/servers/{externalId}/backups", null);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion backup error ({(int)response.StatusCode}): {body}"
                };
            }

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<IEnumerable<BackupInfo>> ListBackupsAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/servers/{externalId}/backups");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var data = root.TryGetProperty("data", out var dataProp) ? dataProp : root;

            var backups = new List<BackupInfo>();

            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    backups.Add(ParseBackupInfo(item));
                }
            }

            return backups;
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
            var response = await _httpClient.PostAsync($"/servers/{externalId}/backups/{backupId}/restore", null);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion restore error ({(int)response.StatusCode}): {body}"
                };
            }

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<ProvisionResult> SendPowerActionAsync(string externalId, string action)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/servers/{externalId}/{action}", null);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"VirtFusion {action} error ({(int)response.StatusCode}): {body}"
                };
            }

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static string GetOptionOrDefault(Dictionary<string, string> options, string key, string defaultValue)
    {
        return options.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : defaultValue;
    }

    private static string? ExtractServerId(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataProp) ? dataProp : root;

        if (data.TryGetProperty("id", out var idProp))
        {
            return idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt64().ToString()
                : idProp.GetString();
        }

        return null;
    }

    private static BackupInfo ParseBackupInfo(JsonElement item)
    {
        var info = new BackupInfo();

        if (item.TryGetProperty("id", out var idProp))
            info.Id = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString() ?? "";

        if (item.TryGetProperty("name", out var nameProp))
            info.Name = nameProp.GetString();

        if (item.TryGetProperty("created", out var createdProp) || item.TryGetProperty("created_at", out createdProp))
        {
            if (DateTime.TryParse(createdProp.GetString(), out var dt))
                info.CreatedAt = dt;
        }

        if (item.TryGetProperty("size", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number)
            info.SizeBytes = sizeProp.GetInt64();

        if (item.TryGetProperty("status", out var statusProp))
            info.Status = statusProp.GetString();

        return info;
    }
}
