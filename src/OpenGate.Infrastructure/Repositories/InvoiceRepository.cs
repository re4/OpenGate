using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class InvoiceRepository(MongoDbContext context) : MongoRepository<Invoice>(context, context.Invoices), IInvoiceRepository
{

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

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await Collection.CountDocumentsAsync(i => i.InvoiceNumber.StartsWith($"INV-{year}-"));
        var sequential = count + 1;
        return $"INV-{year}-{sequential}";
    }
}
