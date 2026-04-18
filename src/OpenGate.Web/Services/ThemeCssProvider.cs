using System.Text;
using System.Text.RegularExpressions;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

/// <summary>
/// Builds the inline <c>:root</c> stylesheet from the active theme. All
/// values written into the document are strictly validated against
/// per-variable allowlists so a malicious admin cannot break out of the
/// <c>&lt;style&gt;</c> element and inject arbitrary HTML or script.
/// </summary>
public class ThemeCssProvider(IServiceScopeFactory scopeFactory) : IThemeCssProvider
{
    private string? _cachedCss;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly Dictionary<string, string> VariableToCssProperty = new(StringComparer.Ordinal)
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

    private static readonly HashSet<string> ColorVariables = new(StringComparer.Ordinal)
    {
        "BgBody","BgSurface","BgElevated","BgInput","Border","BorderLight",
        "Text","TextDim","TextMuted","Accent","AccentHover","AccentMuted",
        "Green","GreenMuted","Yellow","YellowMuted","Red","RedMuted",
        "Blue","BlueMuted","Orange","OrangeMuted"
    };

    private static readonly HashSet<string> RadiusVariables = new(StringComparer.Ordinal) { "Radius", "RadiusLg" };
    private static readonly HashSet<string> FontVariables = new(StringComparer.Ordinal) { "Font", "FontMono" };

    private static readonly Regex HexColorRegex = new("^#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);
    private static readonly Regex RgbaColorRegex = new(
        @"^rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(?:,\s*(?:0|1|0?\.\d+)\s*)?\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FontFamilyRegex = new(
        @"^[A-Za-z0-9 _\-,'""]+$",
        RegexOptions.Compiled);

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

            _cachedCss = theme == null ? string.Empty : BuildCss(theme.Variables);
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

    /// <summary>
    /// Builds CSS text from validated theme variables. Any value that fails
    /// validation is silently dropped so the rendered stylesheet is always
    /// safe to interpolate as an inline <c>&lt;style&gt;</c> body.
    /// </summary>
    private static string BuildCss(Dictionary<string, string> variables)
    {
        if (variables.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(":root {");

        foreach (var (key, rawValue) in variables)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) continue;
            if (!VariableToCssProperty.TryGetValue(key, out var cssProperty)) continue;

            var value = rawValue.Trim();
            if (value.Length > 64) continue;

            if (!TryNormalizeValue(key, value, out var normalized)) continue;

            sb.Append("    ").Append(cssProperty).Append(": ").Append(normalized).AppendLine(";");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Validates and normalizes a CSS variable value based on which kind of
    /// theme variable it represents.
    /// </summary>
    private static bool TryNormalizeValue(string key, string value, out string normalized)
    {
        normalized = string.Empty;

        if (ColorVariables.Contains(key))
        {
            if (HexColorRegex.IsMatch(value) || RgbaColorRegex.IsMatch(value))
            {
                normalized = value;
                return true;
            }
            return false;
        }

        if (RadiusVariables.Contains(key))
        {
            if (int.TryParse(value, out var radius) && radius >= 0 && radius <= 64)
            {
                normalized = $"{radius}px";
                return true;
            }
            return false;
        }

        if (FontVariables.Contains(key))
        {
            if (FontFamilyRegex.IsMatch(value))
            {
                normalized = value;
                return true;
            }
            return false;
        }

        return false;
    }
}
