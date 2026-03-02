# OpenGate

**Open-Source Hosting Billing Platform**

An open-source billing and client management platform for hosting companies, built with C# and .NET 10, OpenGate provides a modern, extensible alternative with Blazor Server UI and MongoDB.

## Features

- **Product Catalog** - Create products with configurable options, billing cycles, and categories
- **Client Storefront** - Browse, configure, and order products with a shopping cart
- **Order Management** - Full lifecycle: pending, active, suspended, cancelled, terminated
- **Invoicing** - Auto-generated invoices with PDF export (QuestPDF)
- **Payment Gateways** - Stripe, PayPal, Heleket, Cryptomus, NOWPayments, and BTCPay Server integrations via extension system
- **Server Provisioning** - Pterodactyl, Proxmox VE, and VirtFusion integrations for automated server management
- **Server Management** - Customer-facing power controls (start/stop/restart), OS reinstall, and backup management
- **Support Tickets** - Client ticket system with priority levels and staff replies
- **Admin Dashboard** - Revenue stats, user management, and global settings
- **Extension System** - Plugin architecture for custom gateways, provisioners, and features
- **Email Notifications** - SMTP-based notifications for invoices, orders, and tickets

## Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 (LTS) |
| Language | C# 14 |
| Backend | ASP.NET Core 10 |
| Frontend | Blazor Server |
| Database | MongoDB |
| Auth | ASP.NET Identity + MongoDbCore |
| PDF | QuestPDF 2026.2 |
| CSS | Bootstrap 5 + Bootstrap Icons |
| Payments | Stripe.net 50.x, PayPal REST API v2, Heleket API, Cryptomus API, NOWPayments API, BTCPay Server Greenfield API |
| Provisioning | Pterodactyl Panel API, Proxmox VE API, VirtFusion API |
| Platform | x64 only |

## Project Structure

```
OpenGate/
  src/
    OpenGate.Domain/                  # Entities, enums, repository interfaces
    OpenGate.Application/             # DTOs, service interfaces, service implementations
    OpenGate.Infrastructure/          # MongoDB repositories, DI registration
    OpenGate.Extensions.Abstractions/ # Extension/plugin contracts
    OpenGate.Web/                     # Blazor Server app, controllers, UI
  extensions/
    OpenGate.Extensions.Stripe/       # Stripe payment gateway
    OpenGate.Extensions.PayPal/       # PayPal payment gateway
    OpenGate.Extensions.Heleket/      # Heleket crypto payment gateway
    OpenGate.Extensions.Cryptomus/    # Cryptomus crypto payment gateway
    OpenGate.Extensions.NowPayments/  # NOWPayments crypto payment gateway
    OpenGate.Extensions.BtcPayServer/ # BTCPay Server payment gateway
    OpenGate.Extensions.Pterodactyl/  # Pterodactyl server provisioner
    OpenGate.Extensions.Proxmox/      # Proxmox VE server provisioner
    OpenGate.Extensions.VirtFusion/   # VirtFusion server provisioner
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [MongoDB](https://www.mongodb.com/try/download/community) (local or hosted)

## Getting Started

1. **Clone the repository**
   ```bash
   git clone <repo-url>
   cd OpenGate
   ```

2. **Configure MongoDB**

   Edit `src/OpenGate.Web/appsettings.json`:
   ```json
   {
     "MongoDB": {
       "ConnectionString": "mongodb://localhost:27017",
       "DatabaseName": "OpenGate"
     }
   }
   ```

3. **Run the application**
   ```bash
   dotnet run --project src/OpenGate.Web
   ```

4. **Access the app**

   Navigate to `https://localhost:5001` (or the port shown in console output).

5. **Default admin account**
   - Email: `admin@opengate.local`
   - Password: `Admin123!`

## Configuration

### Payment Gateways

Configure payment gateways in the Admin Settings panel (`/admin/settings`):

**Stripe:**
- Secret Key
- Publishable Key
- Webhook Secret

**PayPal:**
- Client ID
- Client Secret
- Sandbox mode (true/false)

**Heleket:**
- Merchant ID (UUID)
- API Key

**Cryptomus:**
- Merchant ID (UUID)
- API Key

**NOWPayments:**
- API Key
- IPN Secret (for webhook verification)

**BTCPay Server:**
- Server URL (e.g. `https://btcpay.example.com`)
- API Key (Greenfield API)
- Store ID
- Webhook Secret

### Server Provisioning

**Pterodactyl:**
- Panel URL
- API Key
- Default Nest/Egg/Location IDs

**Proxmox VE:**
- API URL (e.g. `https://proxmox.example.com:8006/api2/json`)
- Token ID (e.g. `user@pam!tokenname`)
- Token Secret
- Default Node, Storage, Template VMID
- Default Memory, Cores, Disk

**VirtFusion:**
- API URL (e.g. `https://virtfusion.example.com/api/v1`)
- API Token (Bearer token)
- Default Operating System ID
- Default Hypervisor Group ID
- Default Package ID

### Email (SMTP)

Configure in Admin Settings under the "Email" group:
- SMTP Host, Port, Username, Password
- From address

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/invoices/{id}/pdf` | Download invoice PDF |
| `POST /api/webhooks/{gatewayName}` | Payment gateway webhooks |

## Customer Server Management

Active orders with a provisioned server show a **Manage Server** button. The server management page provides:

- **Power Controls** - Start, stop, and restart the server
- **Reinstall OS** - Wipe and reinstall with a chosen OS template (with confirmation)
- **Backups** - Create, list, and restore backups
- **Server Info** - Live status, resource usage, and provisioning details

Available at `/my/orders/{orderId}/server` for customers who own the order.

## Extending OpenGate

Create custom extensions by implementing interfaces from `OpenGate.Extensions.Abstractions`:

- `IPaymentGateway` - Custom payment gateways
- `IServerProvisioner` - Custom server provisioning (power, reinstall, backups, lifecycle)
- `IOpenGateExtension` - Base extension interface

## License

MIT
