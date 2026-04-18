# OpenGate

A self-hosted billing and client area for hosting companies. Think WHMCS or Blesta, but open source, written in modern .NET, and without the PHP.

I built this because every billing platform I tried was either expensive, abandoned, or both. OpenGate is what I wished existed: products, orders, invoices, payments, server provisioning, and tickets — in one app that you actually own.

It's still young. Expect rough edges. PRs welcome.

## What it does

- Sell hosting (or anything else with a recurring price). Products have configurable options, billing cycles, categories, taxes.
- A storefront for customers — cart, checkout, order history, invoices.
- Orders move through the lifecycle you'd expect (pending → active → suspended → cancelled → terminated).
- Invoices are generated automatically and exported to PDF via QuestPDF.
- Payments through Stripe, PayPal, BTCPay Server, NOWPayments, Cryptomus, or Heleket. Webhooks are signed-and-verified, idempotent, and rate-limited.
- Server provisioning for Pterodactyl, Proxmox VE, and VirtFusion. Customers can power-cycle, reinstall, and back up their own boxes from the client area.
- Support tickets with priorities, staff replies, and file attachments (scanned with ClamAV if you point it at one).
- An admin dashboard with the usual: revenue, recent orders, user management, and a settings panel that doesn't require restarting the app.
- Email notifications over SMTP.
- An extension model so you can plug in your own gateway or provisioner without forking.

## Stack

Nothing exotic.

- .NET 10 / C# 14 / ASP.NET Core 10
- Blazor Server for the UI (with a handful of MVC controllers for auth, webhooks, and the migration API)
- MongoDB for storage, ASP.NET Identity on top of `AspNetCore.Identity.MongoDbCore`
- QuestPDF for invoices
- Bootstrap 5 for the front-end (no SPA framework, no build pipeline)
- x64 only

## Layout

```
src/
  OpenGate.Domain/                  entities, enums, repository contracts
  OpenGate.Application/             DTOs and services
  OpenGate.Infrastructure/          Mongo repos, DI, index initializer
  OpenGate.Extensions.Abstractions/ extension contracts + shared helpers
  OpenGate.Web/                     the actual app (Blazor + controllers)
extensions/
  OpenGate.Extensions.Stripe/
  OpenGate.Extensions.PayPal/
  OpenGate.Extensions.BtcPayServer/
  OpenGate.Extensions.NowPayments/
  OpenGate.Extensions.Cryptomus/
  OpenGate.Extensions.Heleket/
  OpenGate.Extensions.Pterodactyl/
  OpenGate.Extensions.Proxmox/
  OpenGate.Extensions.VirtFusion/
```

## Getting it running

You'll need:

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A MongoDB you can talk to (local Docker is fine)

Then:

```bash
git clone <this repo>
cd OpenGate
dotnet run --project src/OpenGate.Web
```

Point `MongoDB:ConnectionString` and `MongoDB:DatabaseName` at your database in `src/OpenGate.Web/appsettings.json` (or override with environment variables — `MongoDB__ConnectionString`, etc).

The first time the app starts it will:

1. Create the required Mongo indexes.
2. Seed the `Admin` and `Customer` roles, default settings, themes, and tax rates.
3. Create the bootstrap admin account.

About that admin account — there is **no hardcoded default password anymore**. Set one of these before first run:

```bash
# environment variables
OPENGATE_ADMIN_EMAIL=you@example.com
OPENGATE_ADMIN_PASSWORD=some-long-password-you-actually-remember

# or in appsettings.json
"Bootstrap": {
  "AdminEmail":    "you@example.com",
  "AdminPassword": "some-long-password-you-actually-remember"
}
```

If you don't set one, the app generates a strong random password on first run and writes it to the log **once**. Read it, log in, change it, move on. Don't ignore the log line; you'll have to reset it through Mongo if you do.

## Configuring everything else

Almost all configuration lives in the database, editable from `/admin/settings` while the app is running. Restarts aren't required for changes to settings, gateways, themes, or tax rates.

### Payment gateways

Pick the ones you want and fill in the keys. Each gateway exposes its own settings under the **Payments** tab.

- **Stripe** — secret key, publishable key, webhook secret. Webhook URL: `/api/webhooks/stripe`.
- **PayPal** — client ID, client secret, sandbox toggle, **webhook ID** (required — webhooks are verified against PayPal's `/v1/notifications/verify-webhook-signature`, anything unsigned is rejected). Webhook URL: `/api/webhooks/paypal`.
- **BTCPay Server** — your server URL, Greenfield API key, store ID, webhook secret. If your BTCPay is on a private LAN, set `AllowPrivateHosts` to `true` (off by default — see Security below).
- **NOWPayments** — API key, IPN secret. HMAC-SHA512 signature verified in constant time.
- **Cryptomus / Heleket** — merchant ID, API key. Same constant-time signature checks.

### Server provisioners

- **Pterodactyl** — panel URL, application API key, defaults for nest/egg/location.
- **Proxmox VE** — API URL (`https://host:8006/api2/json`), API token (`user@pam!tokenname` + secret), default node/storage/template VMID, default specs. TLS validation is on by default; flip `AllowSelfSignedCertificate` if you really need it.
- **VirtFusion** — API URL, bearer token, default OS / hypervisor group / package IDs.

For all three, admin-supplied URLs go through an SSRF allowlist that rejects loopback / private / link-local / multicast hosts unless you explicitly opt in with `AllowPrivateHosts`. Proxmox is the common exception (it usually lives on a management LAN), so its default for that flag is `true`. The others default to `false`.

### Email

Standard SMTP — host, port, username, password, from address. Set under the **Email** group in admin settings.

## What customers see

When an order has a provisioned server, a **Manage Server** button shows up. They get:

- Power controls (start / stop / restart)
- Reinstall (with a confirmation step, because mistakes happen)
- Backups (create / list / restore)
- Live status and resource usage

It lives at `/my/orders/{orderId}/server` and is gated by ownership.

## API surface

The HTTP surface is intentionally tiny. Everything else is Blazor.

| Method | Path | What it does |
|--------|------|--------------|
| `GET`  | `/api/invoices/{id}/pdf` | Download invoice PDF (auth required) |
| `POST` | `/api/webhooks/{gateway}` | Inbound webhook for a payment gateway |
| `POST` | `/api/migrate/...` | Admin DB migration helpers (admin-api rate limit) |

## Writing an extension

Drop a class library in `extensions/`, reference `OpenGate.Extensions.Abstractions`, and implement one of:

- `IPaymentGateway` — for a new payment provider. Implement `CreatePaymentAsync`, `HandleWebhookAsync`, `RefundAsync`, etc.
- `IServerProvisioner` — for a new control panel / hypervisor. Implement the lifecycle methods (create, suspend, terminate, power, reinstall, backups).
- `IOpenGateExtension` — the base contract.

Then register it in `Program.cs`:

```csharp
builder.Services.AddScoped<IPaymentGateway, MyGateway>();
```

If your extension talks HTTP to a remote service, take an `IServiceProvider` in the constructor and grab `IHttpClientFactory` from it — the existing gateways do exactly that and reuse the host's `"OpenGate.Default"` client. Validate any admin-supplied URL with `HttpSecurity.TryValidateOutboundUrl` before you use it.

## Security notes

I take this seriously because billing software is a great target. The things that have been done so far:

- Strong password policy and account lockout on failed login.
- Rate limiting on login, register, webhooks, and admin APIs.
- HSTS-friendly security headers and a strict CSP in production.
- All webhook signatures verified in constant time. PayPal goes through the official verification endpoint; Stripe uses `Stripe.EventUtility`; the rest use HMAC + `CryptographicOperations.FixedTimeEquals`.
- Idempotent webhook processing — duplicate deliveries can't double-pay an invoice.
- SSRF guard on every admin-supplied URL (BTCPay, Pterodactyl, Proxmox, VirtFusion).
- Stored XSS hardening on theme CSS variables — anything that isn't a hex color, rgba, integer, or font name is dropped before being injected into a `<style>`.
- SVG uploads are disabled (they execute scripts when rendered as images).
- Ticket attachment URLs are restricted to the local upload folder.
- TLS validation is on by default for every outbound integration; opt in if you need self-signed.

If you find something I missed, please open a private security advisory on GitHub instead of a public issue.

## Upgrading

If you're running the published Docker image via `docker-compose.yml`, upgrades are:

```bash
# bump OPENGATE_VERSION in your .env to the new tag
docker compose pull
docker compose up -d
```

Your data lives in named volumes (`mongo-data`, `mongo-config`, `opengate-uploads`), which are independent of the container — pulling a new image and recreating the app container does **not** touch them. On startup the new version idempotently re-runs index creation and seed checks, so nothing is dropped or reinserted; existing users, orders, invoices, payments, tickets, products, settings, and uploads are preserved as-is.

The one command that **will** wipe your data is `docker compose down -v` (the `-v` flag deletes the named volumes). Don't run that unless you actually mean it. Plain `docker compose down`, `stop`, `restart`, `pull`, and `up -d` are all safe.

Take a Mongo dump before any upgrade if you care about the data:

```bash
docker compose exec mongo \
  mongodump --username "$MONGO_ROOT_USER" --password "$MONGO_ROOT_PASSWORD" \
            --authenticationDatabase admin --archive --gzip \
  > opengate-$(date +%F).archive.gz
```

Downgrades aren't supported — once a newer version has touched the database, going back to an older one is at your own risk.

## Status

It runs. It works. I use it. There are still gaps:

- More tests.
- More themes (the current one is fine but lonely).
- A documented upgrade path between versions.
- Multi-currency display improvements.

## License

MIT. Do whatever you want with it, just don't blame me when something breaks.
