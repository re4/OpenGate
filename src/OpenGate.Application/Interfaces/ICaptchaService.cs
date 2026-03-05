namespace OpenGate.Application.Interfaces;

public class CaptchaConfig
{
    public string Provider { get; set; } = "None";
    public string SiteKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool LoginEnabled { get; set; }
    public bool RegisterEnabled { get; set; }
    public bool TicketEnabled { get; set; }
    public bool IsConfigured => Provider != "None" && !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);

    public string ScriptUrl => Provider switch
    {
        "RecaptchaV2" => "https://www.google.com/recaptcha/api.js",
        "RecaptchaV3" => $"https://www.google.com/recaptcha/api.js?render={SiteKey}",
        "HCaptcha" => "https://js.hcaptcha.com/1/api.js",
        "Turnstile" => "https://challenges.cloudflare.com/turnstile/v0/api.js",
        _ => ""
    };

    public string WidgetClass => Provider switch
    {
        "RecaptchaV2" => "g-recaptcha",
        "HCaptcha" => "h-captcha",
        "Turnstile" => "cf-turnstile",
        _ => ""
    };

    public string ResponseFieldName => Provider switch
    {
        "RecaptchaV2" or "RecaptchaV3" => "g-recaptcha-response",
        "HCaptcha" => "h-captcha-response",
        "Turnstile" => "cf-turnstile-response",
        _ => "captcha-response"
    };

    public string VerifyUrl => Provider switch
    {
        "RecaptchaV2" or "RecaptchaV3" => "https://www.google.com/recaptcha/api/siteverify",
        "HCaptcha" => "https://api.hcaptcha.com/siteverify",
        "Turnstile" => "https://challenges.cloudflare.com/turnstile/v0/siteverify",
        _ => ""
    };
}

public interface ICaptchaService
{
    Task<CaptchaConfig> GetConfigAsync();
    Task<bool> VerifyAsync(string token, string? remoteIp = null);
    void InvalidateCache();
}
