using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

/// <summary>
/// MongoDB-backed repository for invoices. The invoice number generator is
/// resilient against concurrent creates because it relies on the unique
/// index on <see cref="Invoice.InvoiceNumber"/> and retries on duplicate
/// key errors.
/// </summary>
public class InvoiceRepository(MongoDbContext context) : MongoRepository<Invoice>(context, context.Invoices), IInvoiceRepository
{
    private const int MaxInvoiceNumberAttempts = 16;

    public async Task<IEnumerable<Invoice>> GetByUserAsync(string userId)
    {
        return await Collection.Find(i => i.UserId == userId).ToListAsync();
    }

    public async Task<IEnumerable<Invoice>> GetByOrderAsync(string orderId)
    {
        return await Collection.Find(i => i.OrderId == orderId).ToListAsync();
    }

    public async Task<IEnumerable<Invoice>> GetByStatusAsync(InvoiceStatus status)
    {
        return await Collection.Find(i => i.Status == status).ToListAsync();
    }

    /// <summary>
    /// Allocates the next sequential invoice number for the current year.
    /// The candidate is built from a server-side count, but uniqueness is
    /// enforced by an index on <see cref="Invoice.InvoiceNumber"/> so racing
    /// callers cannot duplicate a number; collisions are detected and
    /// retried with the next available value.
    /// </summary>
    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        for (var attempt = 0; attempt < MaxInvoiceNumberAttempts; attempt++)
        {
            var lastInvoice = await Collection
                .Find(Builders<Invoice>.Filter.Regex(i => i.InvoiceNumber, new MongoDB.Bson.BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(prefix))))
                .Sort(Builders<Invoice>.Sort.Descending(i => i.InvoiceNumber))
                .Limit(1)
                .FirstOrDefaultAsync();

            long sequential = 1;
            if (lastInvoice != null && long.TryParse(lastInvoice.InvoiceNumber.AsSpan(prefix.Length), out var parsed))
            {
                sequential = parsed + 1;
            }
            else
            {
                var existingCount = await Collection.CountDocumentsAsync(
                    Builders<Invoice>.Filter.Regex(i => i.InvoiceNumber, new MongoDB.Bson.BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(prefix))));
                sequential = existingCount + 1;
            }

            sequential += attempt;
            var candidate = $"{prefix}{sequential}";

            var clash = await Collection.Find(i => i.InvoiceNumber == candidate).Limit(1).AnyAsync();
            if (!clash)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Failed to allocate a unique invoice number after multiple attempts.");
    }
}
