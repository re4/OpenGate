using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto> CreateAsync(CreatePaymentDto dto);
    Task<PaymentDto?> GetByIdAsync(string id);
    Task<IEnumerable<PaymentDto>> GetByInvoiceAsync(string invoiceId);
    Task<IEnumerable<PaymentDto>> GetByUserAsync(string userId);
    Task<PaymentDto?> ProcessPaymentAsync(string invoiceId, string userId, string gateway, decimal amount, string? transactionId = null);
}
