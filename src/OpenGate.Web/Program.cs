using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Extensions.Abstractions;
using OpenGate.Extensions.BtcPayServer;
using OpenGate.Extensions.Cryptomus;
using OpenGate.Extensions.Heleket;
using OpenGate.Extensions.NowPayments;
using OpenGate.Extensions.PayPal;
using OpenGate.Extensions.Stripe;
using OpenGate.Extensions.Proxmox;
using OpenGate.Extensions.Pterodactyl;
using OpenGate.Extensions.VirtFusion;
using OpenGate.Infrastructure;
using OpenGate.Web.Components;
using OpenGate.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>(
    builder.Configuration["MongoDB:ConnectionString"],
    builder.Configuration["MongoDB:DatabaseName"])
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.Name = "OpenGate.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("register", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));

    options.AddPolicy("webhook", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("admin-api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<ThemeCssProvider>();
builder.Services.AddSingleton<IThemeCssProvider>(sp => sp.GetRequiredService<ThemeCssProvider>());
builder.Services.AddSingleton<BrandingProvider>();
builder.Services.AddSingleton<CurrencyProvider>();
builder.Services.AddSingleton<IClamAvService, ClamAvService>();
builder.Services.AddSingleton<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<FileUploadService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("OpenGate.Default", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenGate/1.0");
});

builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddScoped<IServerProvisioner, VirtFusionProvisioner>();
builder.Services.AddScoped<IServerProvisioner, PterodactylProvisioner>();
builder.Services.AddScoped<IServerProvisioner, ProxmoxProvisioner>();

builder.Services.AddScoped<IPaymentGateway, StripeGateway>();
builder.Services.AddScoped<IPaymentGateway, PayPalGateway>();
builder.Services.AddScoped<IPaymentGateway, HeleketGateway>();
builder.Services.AddScoped<IPaymentGateway, CryptomusGateway>();
builder.Services.AddScoped<IPaymentGateway, NowPaymentsGateway>();
builder.Services.AddScoped<IPaymentGateway, BtcPayServerGateway>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), interest-cohort=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://www.google.com https://www.gstatic.com https://hcaptcha.com https://*.hcaptcha.com https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: https:; " +
        "frame-src 'self' https://www.google.com https://hcaptcha.com https://*.hcaptcha.com https://challenges.cloudflare.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self' https://www.paypal.com https://www.sandbox.paypal.com https://checkout.stripe.com;";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseAntiforgery();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await SeedData.InitializeAsync(app.Services, app.Configuration);

app.Run();
