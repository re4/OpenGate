using MongoDB.Driver;
using OpenGate.Domain.Entities;

namespace OpenGate.Infrastructure.Data;

/// <summary>
/// Creates the MongoDB indexes required by hot query paths and the indexes
/// that enforce uniqueness invariants (invoice numbers, payment transaction
/// ids, theme uniqueness, etc.). Indexes are idempotent so this can run on
/// every startup safely.
/// </summary>
public sealed class MongoIndexInitializer(MongoDbContext context)
{
    /// <summary>
    /// Ensures all required indexes exist. Safe to call repeatedly.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            CreateOrderIndexesAsync(cancellationToken),
            CreateInvoiceIndexesAsync(cancellationToken),
            CreatePaymentIndexesAsync(cancellationToken),
            CreateTicketIndexesAsync(cancellationToken),
            CreateProductIndexesAsync(cancellationToken),
            CreateSettingIndexesAsync(cancellationToken),
            CreateExtensionConfigIndexesAsync(cancellationToken),
            CreateThemeIndexesAsync(cancellationToken),
            CreateTaxRateIndexesAsync(cancellationToken));
    }

    private Task CreateOrderIndexesAsync(CancellationToken ct) =>
        context.Orders.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(o => o.UserId)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(o => o.Status)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(o => o.NextDueDate)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Descending(o => o.CreatedAt)),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Combine(
                    Builders<Order>.IndexKeys.Ascending(o => o.Status),
                    Builders<Order>.IndexKeys.Ascending(o => o.CreatedAt)))
        ], ct);

    private Task CreateInvoiceIndexesAsync(CancellationToken ct) =>
        context.Invoices.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Invoice>(Builders<Invoice>.IndexKeys.Ascending(i => i.UserId)),
            new CreateIndexModel<Invoice>(Builders<Invoice>.IndexKeys.Ascending(i => i.OrderId)),
            new CreateIndexModel<Invoice>(Builders<Invoice>.IndexKeys.Ascending(i => i.Status)),
            new CreateIndexModel<Invoice>(
                Builders<Invoice>.IndexKeys.Ascending(i => i.InvoiceNumber),
                new CreateIndexOptions { Unique = true, Sparse = true })
        ], ct);

    private Task CreatePaymentIndexesAsync(CancellationToken ct) =>
        context.Payments.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Payment>(Builders<Payment>.IndexKeys.Ascending(p => p.InvoiceId)),
            new CreateIndexModel<Payment>(Builders<Payment>.IndexKeys.Ascending(p => p.UserId)),
            new CreateIndexModel<Payment>(
                Builders<Payment>.IndexKeys.Ascending(p => p.TransactionId),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            new CreateIndexModel<Payment>(
                Builders<Payment>.IndexKeys.Combine(
                    Builders<Payment>.IndexKeys.Ascending(p => p.Gateway),
                    Builders<Payment>.IndexKeys.Ascending(p => p.TransactionId)))
        ], ct);

    private Task CreateTicketIndexesAsync(CancellationToken ct) =>
        context.Tickets.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.UserId)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.Status)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Descending(t => t.CreatedAt))
        ], ct);

    private Task CreateProductIndexesAsync(CancellationToken ct) =>
        context.Products.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.CategoryId)),
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.IsActive))
        ], ct);

    private Task CreateSettingIndexesAsync(CancellationToken ct) =>
        context.Settings.Indexes.CreateOneAsync(
            new CreateIndexModel<Setting>(
                Builders<Setting>.IndexKeys.Ascending(s => s.Key),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: ct);

    private Task CreateExtensionConfigIndexesAsync(CancellationToken ct) =>
        context.ExtensionConfigs.Indexes.CreateOneAsync(
            new CreateIndexModel<ExtensionConfig>(
                Builders<ExtensionConfig>.IndexKeys.Ascending(e => e.Name),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: ct);

    private Task CreateThemeIndexesAsync(CancellationToken ct) =>
        context.Themes.Indexes.CreateOneAsync(
            new CreateIndexModel<Theme>(Builders<Theme>.IndexKeys.Ascending(t => t.IsActive)),
            cancellationToken: ct);

    private Task CreateTaxRateIndexesAsync(CancellationToken ct) =>
        context.TaxRates.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<TaxRate>(Builders<TaxRate>.IndexKeys.Ascending(r => r.Country)),
            new CreateIndexModel<TaxRate>(
                Builders<TaxRate>.IndexKeys.Combine(
                    Builders<TaxRate>.IndexKeys.Ascending(r => r.Country),
                    Builders<TaxRate>.IndexKeys.Ascending(r => r.State)))
        ], ct);
}
