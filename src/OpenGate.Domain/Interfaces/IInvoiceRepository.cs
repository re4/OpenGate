using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Interfaces;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<IEnumerable<Invoice>> GetByUserAsync(string userId);
    Task<IEnumerable<Invoice>> GetByOrderAsync(string orderId);
    Task<IEnumerable<Invoice>> GetByStatusAsync(InvoiceStatus status);
    Task<string> GenerateInvoiceNumberAsync();
}
