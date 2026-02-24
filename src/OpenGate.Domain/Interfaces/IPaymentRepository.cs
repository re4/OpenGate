using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetByInvoiceAsync(string invoiceId);
    Task<IEnumerable<Payment>> GetByUserAsync(string userId);
    Task<Payment?> GetByTransactionIdAsync(string transactionId);
}
