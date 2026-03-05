using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class BrandingProvider(IServiceScopeFactory scopeFactory)
{
    private BrandingInfo? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<BrandingInfo> GetAsync()
    {
        if (_cached != null) return _cached;

        await _lock.WaitAsync();
        try
        {
            if (_cached != null) return _cached;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISettingRepository>();

            var siteName = (await repo.GetByKeyAsync("SiteName"))?.Value ?? "OpenGate";
            var logoUrl = (await repo.GetByKeyAsync("LogoUrl"))?.Value ?? "";
            var footerText = (await repo.GetByKeyAsync("FooterText"))?.Value ?? "";

            _cached = new BrandingInfo
            {
                SiteName = string.IsNullOrWhiteSpace(siteName) ? "OpenGate" : siteName,
                LogoUrl = logoUrl.Trim(),
                FooterText = string.IsNullOrWhiteSpace(footerText) ? $"{siteName}" : footerText,
            };

            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateCache() => _cached = null;
}

public class BrandingInfo
{
    public string SiteName { get; set; } = "OpenGate";
    public string LogoUrl { get; set; } = "";
    public string FooterText { get; set; } = "";
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);
}
