using Microsoft.AspNetCore.Identity;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;

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
            new() { Key = "Currency", Value = "USD", Description = "Default currency code (e.g. USD, EUR, GBP)", Group = "General" },
            new() { Key = "CurrencySymbol", Value = "$", Description = "Currency symbol displayed alongside prices", Group = "General" },
            new() { Key = "TaxRate", Value = "0", Description = "Tax rate percentage applied to invoices (0 = disabled)", Group = "General" },
            new() { Key = "TermsOfServiceUrl", Value = "", Description = "Link to terms of service page (optional)", Group = "General" },

            // Email / SMTP
            new() { Key = "SmtpHost", Value = "", Description = "SMTP server hostname", Group = "Email" },
            new() { Key = "SmtpPort", Value = "587", Description = "SMTP server port", Group = "Email" },
            new() { Key = "SmtpUsername", Value = "", Description = "SMTP authentication username", Group = "Email" },
            new() { Key = "SmtpPassword", Value = "", Description = "SMTP authentication password", Group = "Email" },
            new() { Key = "SmtpFromAddress", Value = "noreply@opengate.local", Description = "From address for outgoing emails", Group = "Email" },
            new() { Key = "SmtpFromName", Value = "OpenGate", Description = "From display name for outgoing emails", Group = "Email" },
            new() { Key = "SmtpUseSsl", Value = "true", Description = "Enable TLS/SSL for SMTP connection", Group = "Email" },

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
    }
}
