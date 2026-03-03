using Microsoft.AspNetCore.Identity;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Extensions.Abstractions;
using OpenGate.Extensions.BtcPayServer;
using OpenGate.Extensions.Cryptomus;
using OpenGate.Extensions.Heleket;
using OpenGate.Extensions.NowPayments;
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
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
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
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<ThemeCssProvider>();
builder.Services.AddSingleton<IThemeCssProvider>(sp => sp.GetRequiredService<ThemeCssProvider>());
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddScoped<IServerProvisioner, VirtFusionProvisioner>();
builder.Services.AddScoped<IServerProvisioner, PterodactylProvisioner>();
builder.Services.AddScoped<IServerProvisioner, ProxmoxProvisioner>();

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
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await SeedData.InitializeAsync(app.Services);

app.Run();
