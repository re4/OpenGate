using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto?> GetByIdAsync(string id);
    Task<IEnumerable<InvoiceDto>> GetAllAsync();
    Task<IEnumerable<InvoiceDto>> GetByUserAsync(string userId);
    Task<IEnumerable<InvoiceDto>> GetByOrderAsync(string orderId);
    Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto);
    Task<InvoiceDto?> UpdateAsync(string id, CreateInvoiceDto dto);
    Task<bool> DeleteAsync(string id);
    Task<InvoiceDto> GenerateForOrderAsync(string orderId);
    Task<InvoiceDto?> MarkAsPaidAsync(string id);
}
