using System.Text;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class ThemeCssProvider(IServiceScopeFactory scopeFactory) : IThemeCssProvider
{
    private string? _cachedCss;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly Dictionary<string, string> VariableToCssProperty = new()
    {
        ["BgBody"] = "--bg-body",
        ["BgSurface"] = "--bg-surface",
        ["BgElevated"] = "--bg-elevated",
        ["BgInput"] = "--bg-input",
        ["Border"] = "--border",
        ["BorderLight"] = "--border-light",
        ["Text"] = "--text",
        ["TextDim"] = "--text-dim",
        ["TextMuted"] = "--text-muted",
        ["Accent"] = "--accent",
        ["AccentHover"] = "--accent-hover",
        ["AccentMuted"] = "--accent-muted",
        ["Green"] = "--green",
        ["GreenMuted"] = "--green-muted",
        ["Yellow"] = "--yellow",
        ["YellowMuted"] = "--yellow-muted",
        ["Red"] = "--red",
        ["RedMuted"] = "--red-muted",
        ["Blue"] = "--blue",
        ["BlueMuted"] = "--blue-muted",
        ["Orange"] = "--orange",
        ["OrangeMuted"] = "--orange-muted",
        ["Radius"] = "--radius",
        ["RadiusLg"] = "--radius-lg",
        ["Font"] = "--font",
        ["FontMono"] = "--font-mono",
    };

    public async Task<string> GetCssAsync()
    {
        if (_cachedCss != null) return _cachedCss;

        await _lock.WaitAsync();
        try
        {
            if (_cachedCss != null) return _cachedCss;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IThemeRepository>();
            var theme = await repo.GetActiveAsync();

            _cachedCss = theme == null ? "" : BuildCss(theme.Variables);
            return _cachedCss;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cachedCss = null;
    }

    private static string BuildCss(Dictionary<string, string> variables)
    {
        if (variables.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine(":root {");

        foreach (var (key, value) in variables)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (VariableToCssProperty.TryGetValue(key, out var cssProperty))
            {
                var cssValue = key is "Radius" or "RadiusLg"
                    ? $"{value}px"
                    : value;
                sb.AppendLine($"    {cssProperty}: {cssValue};");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
