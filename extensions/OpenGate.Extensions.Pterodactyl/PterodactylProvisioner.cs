using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Extensions.Pterodactyl;

/// <summary>
/// Pterodactyl Panel server provisioner. Validates the admin-configured
/// PanelUrl against the SSRF allowlist and uses <see cref="IHttpClientFactory"/>
/// when available so the host controls socket lifetimes and DNS rotation.
/// </summary>
public class PterodactylProvisioner : IServerProvisioner
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly Lazy<HttpClient> _fallbackClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
    private Uri? _panelBaseUri;
    private string _apiKey = string.Empty;
    private string _defaultNestId = "1";
    private string _defaultEggId = "1";
    private string _defaultLocationId = "1";
    private string _defaultStartup = "java -Xms128M -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}";

    /// <summary>
    /// Creates the provisioner, resolving an <see cref="IHttpClientFactory"/>
    /// if the host registered one.
    /// </summary>
    public PterodactylProvisioner(IServiceProvider? serviceProvider = null)
    {
        _httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
    }

    public string Name => "pterodactyl";
    public string DisplayName => "Pterodactyl";
    public string Version => "1.0.0";
    public string? Description => "Pterodactyl Panel server provisioning integration";

    public Task InitializeAsync(Dictionary<string, string> settings)
    {
        var panelUrl = settings.GetValueOrDefault("PanelUrl", string.Empty).TrimEnd('/');
        _apiKey = settings.GetValueOrDefault("ApiKey", string.Empty);
        _defaultNestId = settings.GetValueOrDefault("DefaultNestId", "1");
        _defaultEggId = settings.GetValueOrDefault("DefaultEggId", "1");
        _defaultLocationId = settings.GetValueOrDefault("DefaultLocationId", "1");
        _defaultStartup = settings.GetValueOrDefault("DefaultStartup", _defaultStartup);
        var allowPrivate = bool.TryParse(settings.GetValueOrDefault("AllowPrivateHosts", "false"), out var allow) && allow;

        if (!string.IsNullOrEmpty(panelUrl)
            && HttpSecurity.TryValidateOutboundUrl(panelUrl + "/", allowPrivate, out var uri, out _)
            && uri != null)
        {
            _panelBaseUri = uri;
        }
        else
        {
            _panelBaseUri = null;
        }

        return Task.CompletedTask;
    }

    public Dictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>
        {
            ["PanelUrl"] = "https://panel.example.com",
            ["ApiKey"] = "",
            ["DefaultNestId"] = "1",
            ["DefaultEggId"] = "1",
            ["DefaultLocationId"] = "1",
            ["DefaultStartup"] = "java -Xms128M -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}",
            ["AllowPrivateHosts"] = "false"
        };
    }

    private HttpClient CreateClient()
        => _httpClientFactory?.CreateClient("OpenGate.Default") ?? _fallbackClient.Value;

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content = null)
    {
        if (_panelBaseUri == null)
            throw new InvalidOperationException("Pterodactyl PanelUrl is not configured or is not allowed.");

        var request = new HttpRequestMessage(method, new Uri(_panelBaseUri, path.TrimStart('/')));
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");
        request.Headers.TryAddWithoutValidation("Accept", "Application/vnd.pterodactyl.v1+json");
        if (content != null)
            request.Content = content;
        return request;
    }

    public async Task<ProvisionResult> CreateServerAsync(ProvisionRequest request)
    {
        try
        {
            if (!request.Options.TryGetValue("PterodactylUserId", out var userIdStr) || string.IsNullOrEmpty(userIdStr))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "PterodactylUserId is required in Options. The user must exist in Pterodactyl Panel."
                };
            }

            if (!int.TryParse(userIdStr, out var pterodactylUserId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = "PterodactylUserId must be a valid integer."
                };
            }

            var memory = GetOptionAsInt(request.Options, "Memory", 512);
            var disk = GetOptionAsInt(request.Options, "Disk", 1024);
            var cpu = GetOptionAsInt(request.Options, "Cpu", 100);
            var swap = GetOptionAsInt(request.Options, "Swap", 0);
            var io = GetOptionAsInt(request.Options, "Io", 500);
            var databases = GetOptionAsInt(request.Options, "Databases", 0);
            var allocations = GetOptionAsInt(request.Options, "Allocations", 1);

            var nestId = GetOptionAsInt(request.Options, "NestId", int.Parse(_defaultNestId));
            var eggId = GetOptionAsInt(request.Options, "EggId", int.Parse(_defaultEggId));
            var locationId = GetOptionAsInt(request.Options, "LocationId", int.Parse(_defaultLocationId));

            var serverName = string.IsNullOrEmpty(request.ProductName)
                ? $"server-{request.OrderId}"
                : $"{request.ProductName}-{request.OrderId[..Math.Min(8, request.OrderId.Length)]}";

            var startup = request.Options.TryGetValue("Startup", out var startupOpt) && !string.IsNullOrEmpty(startupOpt)
                ? startupOpt
                : _defaultStartup;

            var createPayload = new
            {
                name = serverName,
                user = pterodactylUserId,
                egg = eggId,
                startup,
                external_id = request.OrderId,
                description = $"Order {request.OrderId} - {request.UserEmail}",
                limits = new
                {
                    memory,
                    swap,
                    disk,
                    io,
                    cpu
                },
                feature_limits = new
                {
                    databases,
                    allocations
                },
                deploy = new
                {
                    locations = new[] { locationId },
                    dedicated_ip = false,
                    port_range = Array.Empty<string>()
                },
                start_on_completion = true,
                environment = new Dictionary<string, string>()
            };

            var json = JsonSerializer.Serialize(createPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Post, "/api/application/servers", content));

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Pterodactyl API error ({(int)response.StatusCode}): {responseContent}"
                };
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;
            var attributes = root.TryGetProperty("attributes", out var attrs) ? attrs : root;

            var externalId = attributes.TryGetProperty("id", out var idProp)
                ? idProp.GetInt32().ToString()
                : attributes.TryGetProperty("identifier", out var idProp2)
                    ? idProp2.GetString()
                    : null;

            return new ProvisionResult
            {
                Success = true,
                ExternalId = externalId ?? string.Empty,
                Metadata = new Dictionary<string, string>
                {
                    ["OrderId"] = request.OrderId,
                    ["ServerName"] = serverName
                }
            };
        }
        catch (Exception ex)
        {
            return new ProvisionResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ProvisionResult> SuspendServerAsync(string externalId)
    {
        try
        {
            var serverId = await ResolveServerIdAsync(externalId);
            if (string.IsNullOrEmpty(serverId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Server not found: {externalId}"
                };
            }

            var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Post, $"/api/application/servers/{serverId}/suspend"));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Pterodactyl API error ({(int)response.StatusCode}): {content}"
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
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ProvisionResult> UnsuspendServerAsync(string externalId)
    {
        try
        {
            var serverId = await ResolveServerIdAsync(externalId);
            if (string.IsNullOrEmpty(serverId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Server not found: {externalId}"
                };
            }

            var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Post, $"/api/application/servers/{serverId}/unsuspend"));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Pterodactyl API error ({(int)response.StatusCode}): {content}"
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
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ProvisionResult> TerminateServerAsync(string externalId)
    {
        try
        {
            var serverId = await ResolveServerIdAsync(externalId);
            if (string.IsNullOrEmpty(serverId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Server not found: {externalId}"
                };
            }

            var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Delete, $"/api/application/servers/{serverId}"));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Pterodactyl API error ({(int)response.StatusCode}): {content}"
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
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ServerStatus> GetServerStatusAsync(string externalId)
    {
        try
        {
            var serverId = await ResolveServerIdAsync(externalId);
            if (string.IsNullOrEmpty(serverId))
            {
                return new ServerStatus
                {
                    IsOnline = false,
                    IsSuspended = false,
                    StatusMessage = $"Server not found: {externalId}"
                };
            }

            var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Get, $"/api/application/servers/{serverId}"));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ServerStatus
                {
                    IsOnline = false,
                    IsSuspended = false,
                    StatusMessage = $"API error ({(int)response.StatusCode}): {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var attributes = root.TryGetProperty("attributes", out var attrs) ? attrs : root;

            var isSuspended = attributes.TryGetProperty("suspended", out var suspendedProp) && suspendedProp.GetBoolean();
            var status = attributes.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";

            var resources = new Dictionary<string, object>();

            if (attributes.TryGetProperty("limits", out var limits))
            {
                if (limits.TryGetProperty("memory", out var memProp))
                    resources["Memory"] = memProp.GetInt64();
                if (limits.TryGetProperty("disk", out var diskProp))
                    resources["Disk"] = diskProp.GetInt64();
                if (limits.TryGetProperty("cpu", out var cpuProp))
                    resources["Cpu"] = cpuProp.GetInt64();
            }

            return new ServerStatus
            {
                IsOnline = status is "running" or "starting",
                IsSuspended = isSuspended,
                StatusMessage = status,
                Resources = resources
            };
        }
        catch (Exception ex)
        {
            return new ServerStatus
            {
                IsOnline = false,
                IsSuspended = false,
                StatusMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ProvisionResult> StartServerAsync(string externalId)
    {
        return await SendPowerSignalAsync(externalId, "start");
    }

    public async Task<ProvisionResult> StopServerAsync(string externalId)
    {
        return await SendPowerSignalAsync(externalId, "stop");
    }

    public async Task<ProvisionResult> RestartServerAsync(string externalId)
    {
        return await SendPowerSignalAsync(externalId, "restart");
    }

    public Task<ProvisionResult> ReinstallServerAsync(string externalId, ReinstallOptions options)
    {
        return Task.FromResult(new ProvisionResult
        {
            Success = false,
            ErrorMessage = "Reinstall is not yet supported for Pterodactyl."
        });
    }

    public Task<ProvisionResult> CreateBackupAsync(string externalId)
    {
        return Task.FromResult(new ProvisionResult
        {
            Success = false,
            ErrorMessage = "Backups are not yet supported for Pterodactyl."
        });
    }

    public Task<IEnumerable<BackupInfo>> ListBackupsAsync(string externalId)
    {
        return Task.FromResult(Enumerable.Empty<BackupInfo>());
    }

    public Task<ProvisionResult> RestoreBackupAsync(string externalId, string backupId)
    {
        return Task.FromResult(new ProvisionResult
        {
            Success = false,
            ErrorMessage = "Backup restore is not yet supported for Pterodactyl."
        });
    }

    private async Task<ProvisionResult> SendPowerSignalAsync(string externalId, string signal)
    {
        try
        {
            var serverId = await ResolveServerIdAsync(externalId);
            if (string.IsNullOrEmpty(serverId))
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Server not found: {externalId}"
                };
            }

            var json = JsonSerializer.Serialize(new { signal });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Post, $"/api/client/servers/{serverId}/power", content));
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ProvisionResult
                {
                    Success = false,
                    ErrorMessage = $"Pterodactyl power error ({(int)response.StatusCode}): {body}"
                };
            }

            return new ProvisionResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            return new ProvisionResult { Success = false, ErrorMessage = "An unexpected error occurred. Please try again." };
        }
    }

    private static int GetOptionAsInt(Dictionary<string, string> options, string key, int defaultValue)
    {
        if (options.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;
        return defaultValue;
    }

    private async Task<string?> ResolveServerIdAsync(string externalId)
    {
        try
        {
            if (int.TryParse(externalId, out _))
            {
                var response = await CreateClient().SendAsync(CreateRequest(HttpMethod.Get, $"/api/application/servers/{externalId}"));
                if (response.IsSuccessStatusCode)
                    return externalId;
            }

            var listResponse = await CreateClient().SendAsync(CreateRequest(HttpMethod.Get, $"/api/application/servers?filter[external_id]={Uri.EscapeDataString(externalId)}"));
            var content = await listResponse.Content.ReadAsStringAsync();

            if (!listResponse.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var data = root.TryGetProperty("data", out var dataProp) ? dataProp : root;

            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                var first = data[0];
                var attrs = first.TryGetProperty("attributes", out var a) ? a : first;
                if (attrs.TryGetProperty("id", out var idProp))
                    return idProp.GetInt32().ToString();
            }

            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out var innerData))
            {
                var arr = innerData.EnumerateArray();
                if (arr.MoveNext())
                {
                    var first = arr.Current;
                    var attrs = first.TryGetProperty("attributes", out var a) ? a : first;
                    if (attrs.TryGetProperty("id", out var idProp))
                        return idProp.GetInt32().ToString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
