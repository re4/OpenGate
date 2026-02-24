using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class PaymentService(IPaymentRepository repository, IMapper mapper) : IPaymentService
{
    public async Task<PaymentDto> CreateAsync(CreatePaymentDto dto)
    {
        var entity = mapper.Map<Payment>(dto);
        var created = await repository.CreateAsync(entity);
        return mapper.Map<PaymentDto>(created);
    }

    public async Task<PaymentDto?> GetByIdAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<PaymentDto>(entity);
    }

    public async Task<IEnumerable<PaymentDto>> GetByInvoiceAsync(string invoiceId)
    {
        var entities = await repository.GetByInvoiceAsync(invoiceId);
        return mapper.Map<IEnumerable<PaymentDto>>(entities);
    }

    public async Task<IEnumerable<PaymentDto>> GetByUserAsync(string userId)
    {
        var entities = await repository.GetByUserAsync(userId);
        return mapper.Map<IEnumerable<PaymentDto>>(entities);
    }

    public async Task<PaymentDto?> ProcessPaymentAsync(string invoiceId, string userId, string gateway, decimal amount, string? transactionId = null)
    {
        var payment = new Payment
        {
            InvoiceId = invoiceId,
            UserId = userId,
            Gateway = gateway,
            TransactionId = transactionId,
            Amount = amount,
            Currency = "USD",
            Status = PaymentStatus.Pending
        };

        var created = await repository.CreateAsync(payment);
        return mapper.Map<PaymentDto>(created);
    }
}
