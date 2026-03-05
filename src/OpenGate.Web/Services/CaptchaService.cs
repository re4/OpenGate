using System.Text.Json;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class CaptchaService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory) : ICaptchaService
{
    private CaptchaConfig? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<CaptchaConfig> GetConfigAsync()
    {
        if (_cached != null) return _cached;

        await _lock.WaitAsync();
        try
        {
            if (_cached != null) return _cached;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISettingRepository>();

            var provider = (await repo.GetByKeyAsync("CaptchaProvider"))?.Value ?? "None";
            var siteKey = (await repo.GetByKeyAsync("CaptchaSiteKey"))?.Value ?? "";
            var secretKey = (await repo.GetByKeyAsync("CaptchaSecretKey"))?.Value ?? "";
            var loginEnabled = (await repo.GetByKeyAsync("CaptchaLoginEnabled"))?.Value ?? "false";
            var registerEnabled = (await repo.GetByKeyAsync("CaptchaRegisterEnabled"))?.Value ?? "false";
            var ticketEnabled = (await repo.GetByKeyAsync("CaptchaTicketEnabled"))?.Value ?? "false";

            _cached = new CaptchaConfig
            {
                Provider = string.IsNullOrWhiteSpace(provider) ? "None" : provider.Trim(),
                SiteKey = siteKey.Trim(),
                SecretKey = secretKey.Trim(),
                LoginEnabled = string.Equals(loginEnabled, "true", StringComparison.OrdinalIgnoreCase),
                RegisterEnabled = string.Equals(registerEnabled, "true", StringComparison.OrdinalIgnoreCase),
                TicketEnabled = string.Equals(ticketEnabled, "true", StringComparison.OrdinalIgnoreCase),
            };

            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> VerifyAsync(string token, string? remoteIp = null)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var config = await GetConfigAsync();
        if (!config.IsConfigured) return true;

        try
        {
            var client = httpClientFactory.CreateClient();
            var parameters = new Dictionary<string, string>
            {
                ["secret"] = config.SecretKey,
                ["response"] = token
            };
            if (!string.IsNullOrEmpty(remoteIp))
                parameters["remoteip"] = remoteIp;

            var response = await client.PostAsync(config.VerifyUrl, new FormUrlEncodedContent(parameters));
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("success", out var success))
            {
                if (!success.GetBoolean()) return false;

                // reCAPTCHA v3 returns a score (0.0 - 1.0), require >= 0.5
                if (config.Provider == "RecaptchaV3" && doc.RootElement.TryGetProperty("score", out var score))
                {
                    return score.GetDouble() >= 0.5;
                }

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public void InvalidateCache() => _cached = null;
}
