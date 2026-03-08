using Microsoft.AspNetCore.Identity;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using Theme = OpenGate.Domain.Entities.Theme;

namespace OpenGate.Web.Services;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var settingRepository = scope.ServiceProvider.GetRequiredService<ISettingRepository>();

        string[] roles = { "Admin", "Client" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        var adminEmail = "admin@opengate.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        await SeedSettingsAsync(settingRepository);

        var themeRepository = scope.ServiceProvider.GetRequiredService<IThemeRepository>();
        await SeedThemesAsync(themeRepository);

        var taxRateRepository = scope.ServiceProvider.GetRequiredService<ITaxRateRepository>();
        await SeedTaxRatesAsync(taxRateRepository);

        var extensionConfigRepository = scope.ServiceProvider.GetRequiredService<IExtensionConfigRepository>();
        await SeedExtensionConfigsAsync(extensionConfigRepository, settingRepository);
    }

    private static async Task SeedSettingsAsync(ISettingRepository repository)
    {
        var existing = await repository.GetAllAsync();
        var existingKeys = new HashSet<string>(existing.Select(s => s.Key), StringComparer.OrdinalIgnoreCase);

        var defaults = new List<Setting>
        {
            // General
            new() { Key = "SiteName", Value = "OpenGate", Description = "Display name shown in the navbar and page titles", Group = "General" },
            new() { Key = "SiteUrl", Value = "https://localhost", Description = "Public URL of this installation", Group = "General" },
            new() { Key = "CompanyName", Value = "My Company", Description = "Company name used on invoices and emails", Group = "General" },
            new() { Key = "CompanyAddress", Value = "", Description = "Company address shown on invoices", Group = "General" },
            new() { Key = "Currency", Value = "USD", Description = "Default currency for prices and invoices", Group = "General" },
            new() { Key = "LogoUrl", Value = "", Description = "URL to a logo image displayed in the navbar (leave empty for text-only branding)", Group = "General" },
            new() { Key = "FooterText", Value = "", Description = "Custom footer text (leave empty to show site name)", Group = "General" },
            new() { Key = "TermsOfServiceUrl", Value = "", Description = "Link to terms of service page (optional)", Group = "General" },

            // Uploads
            new() { Key = "MaxUploadSizeGB", Value = "1", Description = "Maximum file upload size in GB (applies to ticket attachments)", Group = "General" },

            // ClamAV
            new() { Key = "ClamAvEnabled", Value = "false", Description = "Enable ClamAV antivirus scanning on file uploads", Group = "General" },
            new() { Key = "ClamAvHost", Value = "127.0.0.1", Description = "ClamAV daemon (clamd) hostname", Group = "General" },
            new() { Key = "ClamAvPort", Value = "3310", Description = "ClamAV daemon (clamd) TCP port", Group = "General" },

            // CAPTCHA
            new() { Key = "CaptchaProvider", Value = "None", Description = "CAPTCHA provider: None, RecaptchaV2, RecaptchaV3, HCaptcha, Turnstile", Group = "General" },
            new() { Key = "CaptchaSiteKey", Value = "", Description = "CAPTCHA site/public key", Group = "General" },
            new() { Key = "CaptchaSecretKey", Value = "", Description = "CAPTCHA secret/private key", Group = "General" },
            new() { Key = "CaptchaLoginEnabled", Value = "false", Description = "Show CAPTCHA on login page", Group = "General" },
            new() { Key = "CaptchaRegisterEnabled", Value = "false", Description = "Show CAPTCHA on registration page", Group = "General" },
            new() { Key = "CaptchaTicketEnabled", Value = "false", Description = "Show CAPTCHA on ticket creation", Group = "General" },

            // Email
            new() { Key = "EmailProvider", Value = "SMTP", Description = "Email delivery provider: SMTP or SendGrid", Group = "Email" },
            new() { Key = "EmailFromAddress", Value = "noreply@opengate.local", Description = "From address for outgoing emails", Group = "Email" },
            new() { Key = "EmailFromName", Value = "OpenGate", Description = "From display name for outgoing emails", Group = "Email" },

            // Email – SMTP
            new() { Key = "SmtpHost", Value = "", Description = "SMTP server hostname", Group = "Email" },
            new() { Key = "SmtpPort", Value = "587", Description = "SMTP server port", Group = "Email" },
            new() { Key = "SmtpUsername", Value = "", Description = "SMTP authentication username", Group = "Email" },
            new() { Key = "SmtpPassword", Value = "", Description = "SMTP authentication password", Group = "Email" },
            new() { Key = "SmtpUseSsl", Value = "true", Description = "Enable TLS/SSL for SMTP connection", Group = "Email" },

            // Email – SendGrid
            new() { Key = "SendGridApiKey", Value = "", Description = "SendGrid API key for email delivery", Group = "Email" },

            // Payment
            new() { Key = "StripeEnabled", Value = "false", Description = "Enable Stripe payment gateway", Group = "Payment" },
            new() { Key = "StripePublicKey", Value = "", Description = "Stripe publishable API key", Group = "Payment" },
            new() { Key = "StripeSecretKey", Value = "", Description = "Stripe secret API key", Group = "Payment" },
            new() { Key = "StripeWebhookSecret", Value = "", Description = "Stripe webhook signing secret", Group = "Payment" },
            new() { Key = "PayPalEnabled", Value = "false", Description = "Enable PayPal payment gateway", Group = "Payment" },
            new() { Key = "PayPalClientId", Value = "", Description = "PayPal REST API client ID", Group = "Payment" },
            new() { Key = "PayPalClientSecret", Value = "", Description = "PayPal REST API client secret", Group = "Payment" },
            new() { Key = "PayPalSandbox", Value = "true", Description = "Use PayPal sandbox environment", Group = "Payment" },

            // Payment - Heleket
            new() { Key = "HeleketEnabled", Value = "false", Description = "Enable Heleket cryptocurrency payment gateway", Group = "Payment" },
            new() { Key = "HeleketMerchantId", Value = "", Description = "Heleket merchant UUID", Group = "Payment" },
            new() { Key = "HeleketApiKey", Value = "", Description = "Heleket API key for signing requests", Group = "Payment" },

            // Payment - Cryptomus
            new() { Key = "CryptomusEnabled", Value = "false", Description = "Enable Cryptomus cryptocurrency payment gateway", Group = "Payment" },
            new() { Key = "CryptomusMerchantId", Value = "", Description = "Cryptomus merchant UUID", Group = "Payment" },
            new() { Key = "CryptomusApiKey", Value = "", Description = "Cryptomus API payment key for signing requests", Group = "Payment" },

            // Payment - NOWPayments
            new() { Key = "NowPaymentsEnabled", Value = "false", Description = "Enable NOWPayments cryptocurrency payment gateway", Group = "Payment" },
            new() { Key = "NowPaymentsApiKey", Value = "", Description = "NOWPayments API key", Group = "Payment" },
            new() { Key = "NowPaymentsIpnSecret", Value = "", Description = "NOWPayments IPN secret key for webhook verification", Group = "Payment" },

            // Payment - BTCPay Server
            new() { Key = "BtcPayServerEnabled", Value = "false", Description = "Enable BTCPay Server payment gateway", Group = "Payment" },
            new() { Key = "BtcPayServerUrl", Value = "", Description = "BTCPay Server instance URL (e.g. https://btcpay.example.com)", Group = "Payment" },
            new() { Key = "BtcPayServerApiKey", Value = "", Description = "BTCPay Server Greenfield API key", Group = "Payment" },
            new() { Key = "BtcPayServerStoreId", Value = "", Description = "BTCPay Server store ID", Group = "Payment" },
            new() { Key = "BtcPayServerWebhookSecret", Value = "", Description = "BTCPay Server webhook secret for signature verification", Group = "Payment" },

            // Provisioning - Pterodactyl
            new() { Key = "PterodactylEnabled", Value = "false", Description = "Enable Pterodactyl server provisioning", Group = "Provisioning" },
            new() { Key = "PterodactylUrl", Value = "", Description = "Pterodactyl panel URL (e.g. https://panel.example.com)", Group = "Provisioning" },
            new() { Key = "PterodactylApiKey", Value = "", Description = "Pterodactyl application API key", Group = "Provisioning" },

            // Provisioning - Proxmox
            new() { Key = "ProxmoxEnabled", Value = "false", Description = "Enable Proxmox VE server provisioning", Group = "Provisioning" },
            new() { Key = "ProxmoxApiUrl", Value = "", Description = "Proxmox API URL (e.g. https://proxmox.example.com:8006/api2/json)", Group = "Provisioning" },
            new() { Key = "ProxmoxTokenId", Value = "", Description = "API token ID (e.g. user@pam!tokenname)", Group = "Provisioning" },
            new() { Key = "ProxmoxTokenSecret", Value = "", Description = "API token secret (UUID)", Group = "Provisioning" },
            new() { Key = "ProxmoxDefaultNode", Value = "pve", Description = "Default Proxmox node name", Group = "Provisioning" },
            new() { Key = "ProxmoxDefaultStorage", Value = "local", Description = "Default storage for backups and disks", Group = "Provisioning" },
            new() { Key = "ProxmoxDefaultTemplateId", Value = "", Description = "Default VM template VMID to clone from", Group = "Provisioning" },
            new() { Key = "ProxmoxDefaultMemory", Value = "2048", Description = "Default memory (MB) for new VMs", Group = "Provisioning" },
            new() { Key = "ProxmoxDefaultCores", Value = "1", Description = "Default CPU cores for new VMs", Group = "Provisioning" },
            new() { Key = "ProxmoxDefaultDisk", Value = "32", Description = "Default disk size (GB) for new VMs", Group = "Provisioning" },

            // Provisioning - VirtFusion
            new() { Key = "VirtFusionEnabled", Value = "false", Description = "Enable VirtFusion server provisioning", Group = "Provisioning" },
            new() { Key = "VirtFusionApiUrl", Value = "", Description = "VirtFusion API URL (e.g. https://virtfusion.example.com/api/v1)", Group = "Provisioning" },
            new() { Key = "VirtFusionApiToken", Value = "", Description = "VirtFusion API bearer token", Group = "Provisioning" },
            new() { Key = "VirtFusionDefaultOperatingSystemId", Value = "1", Description = "Default OS template ID for new servers", Group = "Provisioning" },
            new() { Key = "VirtFusionDefaultHypervisorGroupId", Value = "1", Description = "Default hypervisor group ID", Group = "Provisioning" },
            new() { Key = "VirtFusionDefaultPackageId", Value = "1", Description = "Default resource package ID", Group = "Provisioning" },
        };

        foreach (var setting in defaults)
        {
            if (!existingKeys.Contains(setting.Key))
            {
                await repository.CreateAsync(setting);
            }
        }

        string[] deprecated = ["CurrencySymbol", "SmtpFromAddress", "SmtpFromName", "SmtpFrom", "SmtpUser", "TaxRate", "TaxLabel", "TaxInclusive"];
        foreach (var key in deprecated)
        {
            var old = existing.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (old != null)
                await repository.DeleteAsync(old.Id);
        }
    }

    private static readonly (string Name, string DisplayName, ExtensionType Type, string Prefix)[] KnownExtensions =
    [
        ("stripe", "Stripe", ExtensionType.PaymentGateway, "Stripe"),
        ("paypal", "PayPal", ExtensionType.PaymentGateway, "PayPal"),
        ("heleket", "Heleket", ExtensionType.PaymentGateway, "Heleket"),
        ("cryptomus", "Cryptomus", ExtensionType.PaymentGateway, "Cryptomus"),
        ("nowpayments", "NOWPayments", ExtensionType.PaymentGateway, "NowPayments"),
        ("btcpayserver", "BTCPay Server", ExtensionType.PaymentGateway, "BtcPayServer"),
        ("pterodactyl", "Pterodactyl", ExtensionType.ServerProvisioner, "Pterodactyl"),
        ("proxmox", "Proxmox VE", ExtensionType.ServerProvisioner, "Proxmox"),
        ("virtfusion", "VirtFusion", ExtensionType.ServerProvisioner, "VirtFusion"),
    ];

    private static async Task SeedExtensionConfigsAsync(IExtensionConfigRepository extensionRepo, ISettingRepository settingRepo)
    {
        var allSettings = (await settingRepo.GetAllAsync()).ToList();
        var settingsLookup = allSettings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var ext in KnownExtensions)
        {
            var enabledKey = $"{ext.Prefix}Enabled";
            var isEnabled = settingsLookup.TryGetValue(enabledKey, out var enabledVal)
                && string.Equals(enabledVal, "true", StringComparison.OrdinalIgnoreCase);

            var settings = allSettings
                .Where(s => s.Key.StartsWith(ext.Prefix, StringComparison.OrdinalIgnoreCase)
                    && !s.Key.Equals(enabledKey, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(s => s.Key[ext.Prefix.Length..], s => s.Value);

            var existing = await extensionRepo.GetByNameAsync(ext.Name);
            if (existing == null)
            {
                await extensionRepo.CreateAsync(new ExtensionConfig
                {
                    Name = ext.Name,
                    DisplayName = ext.DisplayName,
                    Type = ext.Type,
                    IsEnabled = isEnabled,
                    Settings = settings
                });
            }
            else
            {
                existing.IsEnabled = isEnabled;
                existing.Settings = settings;
                await extensionRepo.UpdateAsync(existing);
            }
        }
    }

    private static async Task SeedThemesAsync(IThemeRepository repository)
    {
        var existing = await repository.GetAllAsync();
        if (existing.Any()) return;

        var presets = new List<Theme>
        {
            new()
            {
                Name = "Default Dark",
                IsActive = true,
                IsPreset = true,
                Variables = new Dictionary<string, string>
                {
                    ["BgBody"] = "#0e0e10",
                    ["BgSurface"] = "#18181b",
                    ["BgElevated"] = "#1f1f23",
                    ["BgInput"] = "#26262b",
                    ["Border"] = "#2e2e33",
                    ["BorderLight"] = "#3a3a40",
                    ["Text"] = "#e4e4e7",
                    ["TextDim"] = "#a1a1aa",
                    ["TextMuted"] = "#71717a",
                    ["Accent"] = "#6366f1",
                    ["AccentHover"] = "#818cf8",
                    ["AccentMuted"] = "rgba(99, 102, 241, 0.15)",
                    ["Green"] = "#22c55e",
                    ["GreenMuted"] = "rgba(34, 197, 94, 0.15)",
                    ["Yellow"] = "#eab308",
                    ["YellowMuted"] = "rgba(234, 179, 8, 0.12)",
                    ["Red"] = "#ef4444",
                    ["RedMuted"] = "rgba(239, 68, 68, 0.12)",
                    ["Blue"] = "#3b82f6",
                    ["BlueMuted"] = "rgba(59, 130, 246, 0.12)",
                    ["Orange"] = "#f97316",
                    ["OrangeMuted"] = "rgba(249, 115, 22, 0.12)",
                    ["Radius"] = "8",
                    ["RadiusLg"] = "12",
                }
            },
            new()
            {
                Name = "Midnight",
                IsActive = false,
                IsPreset = true,
                Variables = new Dictionary<string, string>
                {
                    ["BgBody"] = "#0b0f1a",
                    ["BgSurface"] = "#111827",
                    ["BgElevated"] = "#1e293b",
                    ["BgInput"] = "#1e293b",
                    ["Border"] = "#334155",
                    ["BorderLight"] = "#475569",
                    ["Text"] = "#f1f5f9",
                    ["TextDim"] = "#94a3b8",
                    ["TextMuted"] = "#64748b",
                    ["Accent"] = "#38bdf8",
                    ["AccentHover"] = "#7dd3fc",
                    ["AccentMuted"] = "rgba(56, 189, 248, 0.15)",
                    ["Green"] = "#34d399",
                    ["GreenMuted"] = "rgba(52, 211, 153, 0.15)",
                    ["Yellow"] = "#fbbf24",
                    ["YellowMuted"] = "rgba(251, 191, 36, 0.12)",
                    ["Red"] = "#f87171",
                    ["RedMuted"] = "rgba(248, 113, 113, 0.12)",
                    ["Blue"] = "#60a5fa",
                    ["BlueMuted"] = "rgba(96, 165, 250, 0.12)",
                    ["Orange"] = "#fb923c",
                    ["OrangeMuted"] = "rgba(251, 146, 60, 0.12)",
                    ["Radius"] = "8",
                    ["RadiusLg"] = "12",
                }
            },
            new()
            {
                Name = "Emerald",
                IsActive = false,
                IsPreset = true,
                Variables = new Dictionary<string, string>
                {
                    ["BgBody"] = "#0a0f0d",
                    ["BgSurface"] = "#111916",
                    ["BgElevated"] = "#1a2420",
                    ["BgInput"] = "#1e2b25",
                    ["Border"] = "#2d3d35",
                    ["BorderLight"] = "#3d5248",
                    ["Text"] = "#e2efe8",
                    ["TextDim"] = "#9cb5a8",
                    ["TextMuted"] = "#6e8a7c",
                    ["Accent"] = "#10b981",
                    ["AccentHover"] = "#34d399",
                    ["AccentMuted"] = "rgba(16, 185, 129, 0.15)",
                    ["Green"] = "#22c55e",
                    ["GreenMuted"] = "rgba(34, 197, 94, 0.15)",
                    ["Yellow"] = "#eab308",
                    ["YellowMuted"] = "rgba(234, 179, 8, 0.12)",
                    ["Red"] = "#ef4444",
                    ["RedMuted"] = "rgba(239, 68, 68, 0.12)",
                    ["Blue"] = "#3b82f6",
                    ["BlueMuted"] = "rgba(59, 130, 246, 0.12)",
                    ["Orange"] = "#f97316",
                    ["OrangeMuted"] = "rgba(249, 115, 22, 0.12)",
                    ["Radius"] = "10",
                    ["RadiusLg"] = "14",
                }
            },
            new()
            {
                Name = "Rose",
                IsActive = false,
                IsPreset = true,
                Variables = new Dictionary<string, string>
                {
                    ["BgBody"] = "#100b10",
                    ["BgSurface"] = "#1a1220",
                    ["BgElevated"] = "#241a2e",
                    ["BgInput"] = "#2a1f33",
                    ["Border"] = "#3d2d4a",
                    ["BorderLight"] = "#523d63",
                    ["Text"] = "#f0e4f5",
                    ["TextDim"] = "#b8a0c8",
                    ["TextMuted"] = "#8a6e9c",
                    ["Accent"] = "#f43f5e",
                    ["AccentHover"] = "#fb7185",
                    ["AccentMuted"] = "rgba(244, 63, 94, 0.15)",
                    ["Green"] = "#22c55e",
                    ["GreenMuted"] = "rgba(34, 197, 94, 0.15)",
                    ["Yellow"] = "#eab308",
                    ["YellowMuted"] = "rgba(234, 179, 8, 0.12)",
                    ["Red"] = "#ef4444",
                    ["RedMuted"] = "rgba(239, 68, 68, 0.12)",
                    ["Blue"] = "#3b82f6",
                    ["BlueMuted"] = "rgba(59, 130, 246, 0.12)",
                    ["Orange"] = "#f97316",
                    ["OrangeMuted"] = "rgba(249, 115, 22, 0.12)",
                    ["Radius"] = "8",
                    ["RadiusLg"] = "12",
                }
            },
            new()
            {
                Name = "Sunset",
                IsActive = false,
                IsPreset = true,
                Variables = new Dictionary<string, string>
                {
                    ["BgBody"] = "#120d08",
                    ["BgSurface"] = "#1c150e",
                    ["BgElevated"] = "#271e14",
                    ["BgInput"] = "#2e2418",
                    ["Border"] = "#3e3020",
                    ["BorderLight"] = "#55422d",
                    ["Text"] = "#f5efe6",
                    ["TextDim"] = "#c4b49e",
                    ["TextMuted"] = "#8f7e68",
                    ["Accent"] = "#f97316",
                    ["AccentHover"] = "#fb923c",
                    ["AccentMuted"] = "rgba(249, 115, 22, 0.15)",
                    ["Green"] = "#22c55e",
                    ["GreenMuted"] = "rgba(34, 197, 94, 0.15)",
                    ["Yellow"] = "#eab308",
                    ["YellowMuted"] = "rgba(234, 179, 8, 0.12)",
                    ["Red"] = "#ef4444",
                    ["RedMuted"] = "rgba(239, 68, 68, 0.12)",
                    ["Blue"] = "#3b82f6",
                    ["BlueMuted"] = "rgba(59, 130, 246, 0.12)",
                    ["Orange"] = "#f97316",
                    ["OrangeMuted"] = "rgba(249, 115, 22, 0.12)",
                    ["Radius"] = "6",
                    ["RadiusLg"] = "10",
                }
            },
        };

        foreach (var theme in presets)
        {
            await repository.CreateAsync(theme);
        }
    }

    private static async Task SeedTaxRatesAsync(ITaxRateRepository repository)
    {
        var existing = await repository.GetAllAsync();
        if (existing.Any()) return;

        var rates = new List<TaxRate>
        {
            // ── EU VAT ──
            new() { Country = "AT", CountryName = "Austria", Rate = 20m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "BE", CountryName = "Belgium", Rate = 21m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "BG", CountryName = "Bulgaria", Rate = 20m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "HR", CountryName = "Croatia", Rate = 25m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "CY", CountryName = "Cyprus", Rate = 19m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "CZ", CountryName = "Czechia", Rate = 21m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "DK", CountryName = "Denmark", Rate = 25m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "EE", CountryName = "Estonia", Rate = 22m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "FI", CountryName = "Finland", Rate = 25.5m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "FR", CountryName = "France", Rate = 20m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "DE", CountryName = "Germany", Rate = 19m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "GR", CountryName = "Greece", Rate = 24m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "HU", CountryName = "Hungary", Rate = 27m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "IE", CountryName = "Ireland", Rate = 23m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "IT", CountryName = "Italy", Rate = 22m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "LV", CountryName = "Latvia", Rate = 21m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "LT", CountryName = "Lithuania", Rate = 21m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "LU", CountryName = "Luxembourg", Rate = 17m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "MT", CountryName = "Malta", Rate = 18m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "NL", CountryName = "Netherlands", Rate = 21m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "PL", CountryName = "Poland", Rate = 23m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "PT", CountryName = "Portugal", Rate = 23m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "RO", CountryName = "Romania", Rate = 19m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "SK", CountryName = "Slovakia", Rate = 23m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "SI", CountryName = "Slovenia", Rate = 22m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "ES", CountryName = "Spain", Rate = 21m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "SE", CountryName = "Sweden", Rate = 25m, Label = "VAT", Inclusive = true, Enabled = true },

            // ── Other countries with VAT / GST ──
            new() { Country = "GB", CountryName = "United Kingdom", Rate = 20m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "NO", CountryName = "Norway", Rate = 25m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "CH", CountryName = "Switzerland", Rate = 8.1m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "IS", CountryName = "Iceland", Rate = 24m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "AU", CountryName = "Australia", Rate = 10m, Label = "GST", Inclusive = true, Enabled = true },
            new() { Country = "NZ", CountryName = "New Zealand", Rate = 15m, Label = "GST", Inclusive = true, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", Rate = 5m, Label = "GST", Inclusive = true, Enabled = true },
            new() { Country = "IN", CountryName = "India", Rate = 18m, Label = "GST", Inclusive = true, Enabled = true },
            new() { Country = "SG", CountryName = "Singapore", Rate = 9m, Label = "GST", Inclusive = true, Enabled = true },
            new() { Country = "JP", CountryName = "Japan", Rate = 10m, Label = "Consumption Tax", Inclusive = true, Enabled = true },
            new() { Country = "KR", CountryName = "South Korea", Rate = 10m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "ZA", CountryName = "South Africa", Rate = 15m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "BR", CountryName = "Brazil", Rate = 17m, Label = "ICMS", Inclusive = true, Enabled = true },
            new() { Country = "MX", CountryName = "Mexico", Rate = 16m, Label = "IVA", Inclusive = true, Enabled = true },
            new() { Country = "AR", CountryName = "Argentina", Rate = 21m, Label = "IVA", Inclusive = true, Enabled = true },
            new() { Country = "CL", CountryName = "Chile", Rate = 19m, Label = "IVA", Inclusive = true, Enabled = true },
            new() { Country = "CO", CountryName = "Colombia", Rate = 19m, Label = "IVA", Inclusive = true, Enabled = true },
            new() { Country = "TR", CountryName = "Turkey", Rate = 20m, Label = "KDV", Inclusive = true, Enabled = true },
            new() { Country = "AE", CountryName = "United Arab Emirates", Rate = 5m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "SA", CountryName = "Saudi Arabia", Rate = 15m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "IL", CountryName = "Israel", Rate = 17m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "TH", CountryName = "Thailand", Rate = 7m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "PH", CountryName = "Philippines", Rate = 12m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "MY", CountryName = "Malaysia", Rate = 8m, Label = "SST", Inclusive = true, Enabled = true },
            new() { Country = "ID", CountryName = "Indonesia", Rate = 11m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "RU", CountryName = "Russia", Rate = 20m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "UA", CountryName = "Ukraine", Rate = 20m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "EG", CountryName = "Egypt", Rate = 14m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "NG", CountryName = "Nigeria", Rate = 7.5m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "KE", CountryName = "Kenya", Rate = 16m, Label = "VAT", Inclusive = true, Enabled = true },
            new() { Country = "TW", CountryName = "Taiwan", Rate = 5m, Label = "VAT", Inclusive = true, Enabled = true },

            // ── US – federal (no sales tax) ──
            new() { Country = "US", CountryName = "United States", Rate = 0m, Label = "Sales Tax", Inclusive = false, Enabled = true },

            // ── US state sales tax ──
            new() { Country = "US", CountryName = "United States", State = "AL", StateName = "Alabama", Rate = 4m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "AK", StateName = "Alaska", Rate = 0m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "AZ", StateName = "Arizona", Rate = 5.6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "AR", StateName = "Arkansas", Rate = 6.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "CA", StateName = "California", Rate = 7.25m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "CO", StateName = "Colorado", Rate = 2.9m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "CT", StateName = "Connecticut", Rate = 6.35m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "DE", StateName = "Delaware", Rate = 0m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "FL", StateName = "Florida", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "GA", StateName = "Georgia", Rate = 4m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "HI", StateName = "Hawaii", Rate = 4m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "ID", StateName = "Idaho", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "IL", StateName = "Illinois", Rate = 6.25m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "IN", StateName = "Indiana", Rate = 7m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "IA", StateName = "Iowa", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "KS", StateName = "Kansas", Rate = 6.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "KY", StateName = "Kentucky", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "LA", StateName = "Louisiana", Rate = 4.45m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "ME", StateName = "Maine", Rate = 5.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MD", StateName = "Maryland", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MA", StateName = "Massachusetts", Rate = 6.25m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MI", StateName = "Michigan", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MN", StateName = "Minnesota", Rate = 6.875m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MS", StateName = "Mississippi", Rate = 7m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MO", StateName = "Missouri", Rate = 4.225m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "MT", StateName = "Montana", Rate = 0m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NE", StateName = "Nebraska", Rate = 5.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NV", StateName = "Nevada", Rate = 6.85m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NH", StateName = "New Hampshire", Rate = 0m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NJ", StateName = "New Jersey", Rate = 6.625m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NM", StateName = "New Mexico", Rate = 5.125m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NY", StateName = "New York", Rate = 4m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "NC", StateName = "North Carolina", Rate = 4.75m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "ND", StateName = "North Dakota", Rate = 5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "OH", StateName = "Ohio", Rate = 5.75m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "OK", StateName = "Oklahoma", Rate = 4.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "OR", StateName = "Oregon", Rate = 0m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "PA", StateName = "Pennsylvania", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "RI", StateName = "Rhode Island", Rate = 7m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "SC", StateName = "South Carolina", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "SD", StateName = "South Dakota", Rate = 4.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "TN", StateName = "Tennessee", Rate = 7m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "TX", StateName = "Texas", Rate = 6.25m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "UT", StateName = "Utah", Rate = 6.1m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "VT", StateName = "Vermont", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "VA", StateName = "Virginia", Rate = 5.3m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "WA", StateName = "Washington", Rate = 6.5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "WV", StateName = "West Virginia", Rate = 6m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "WI", StateName = "Wisconsin", Rate = 5m, Label = "Sales Tax", Inclusive = false, Enabled = true },
            new() { Country = "US", CountryName = "United States", State = "WY", StateName = "Wyoming", Rate = 4m, Label = "Sales Tax", Inclusive = false, Enabled = true },

            // ── Canadian provinces ──
            new() { Country = "CA", CountryName = "Canada", State = "AB", StateName = "Alberta", Rate = 5m, Label = "GST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "BC", StateName = "British Columbia", Rate = 12m, Label = "GST+PST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "MB", StateName = "Manitoba", Rate = 12m, Label = "GST+PST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "NB", StateName = "New Brunswick", Rate = 15m, Label = "HST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "NL", StateName = "Newfoundland", Rate = 15m, Label = "HST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "NS", StateName = "Nova Scotia", Rate = 15m, Label = "HST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "ON", StateName = "Ontario", Rate = 13m, Label = "HST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "PE", StateName = "Prince Edward Island", Rate = 15m, Label = "HST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "QC", StateName = "Quebec", Rate = 14.975m, Label = "GST+QST", Inclusive = false, Enabled = true },
            new() { Country = "CA", CountryName = "Canada", State = "SK", StateName = "Saskatchewan", Rate = 11m, Label = "GST+PST", Inclusive = false, Enabled = true },
        };

        foreach (var rate in rates)
        {
            await repository.CreateAsync(rate);
        }
    }
}
