using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class PaymentRepository(MongoDbContext context) : MongoRepository<Payment>(context, context.Payments), IPaymentRepository
{

    public async Task<IEnumerable<Payment>> GetByInvoiceAsync(string invoiceId)
    {
        return await Collection.Find(p => p.InvoiceId == invoiceId).ToListAsync();
    }

    public async Task<IEnumerable<Payment>> GetByUserAsync(string userId)
    {
        return await Collection.Find(p => p.UserId == userId).ToListAsync();
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId)
    {
        return await Collection.Find(p => p.TransactionId == transactionId).FirstOrDefaultAsync();
    }
}
