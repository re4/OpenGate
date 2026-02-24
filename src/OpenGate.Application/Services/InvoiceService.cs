using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class InvoiceService(IInvoiceRepository invoiceRepository, IOrderRepository orderRepository, IMapper mapper) : IInvoiceService
{
    public async Task<InvoiceDto?> GetByIdAsync(string id)
    {
        var entity = await invoiceRepository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<InvoiceDto>(entity);
    }

    public async Task<IEnumerable<InvoiceDto>> GetAllAsync()
    {
        var entities = await invoiceRepository.GetAllAsync();
        return mapper.Map<IEnumerable<InvoiceDto>>(entities);
    }

    public async Task<IEnumerable<InvoiceDto>> GetByUserAsync(string userId)
    {
        var entities = await invoiceRepository.GetByUserAsync(userId);
        return mapper.Map<IEnumerable<InvoiceDto>>(entities);
    }

    public async Task<IEnumerable<InvoiceDto>> GetByOrderAsync(string orderId)
    {
        var entities = await invoiceRepository.GetByOrderAsync(orderId);
        return mapper.Map<IEnumerable<InvoiceDto>>(entities);
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto)
    {
        var entity = mapper.Map<Invoice>(dto);
        var created = await invoiceRepository.CreateAsync(entity);
        return mapper.Map<InvoiceDto>(created);
    }

    public async Task<InvoiceDto?> UpdateAsync(string id, CreateInvoiceDto dto)
    {
        var entity = await invoiceRepository.GetByIdAsync(id);
        if (entity == null) return null;

        mapper.Map(dto, entity);
        await invoiceRepository.UpdateAsync(entity);
        return mapper.Map<InvoiceDto>(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await invoiceRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await invoiceRepository.DeleteAsync(id);
        return true;
    }

    public async Task<InvoiceDto> GenerateForOrderAsync(string orderId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException($"Order with id '{orderId}' not found.");

        var invoiceNumber = await invoiceRepository.GenerateInvoiceNumberAsync();

        var lines = order.Items.Select(item => new InvoiceLine
        {
            Description = $"{item.ProductName} x{item.Quantity}",
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Total = item.Total
        }).ToList();

        var subtotal = order.Total;
        var tax = 0m;
        var total = subtotal + tax;

        var invoice = new Invoice
        {
            UserId = order.UserId,
            OrderId = order.Id,
            InvoiceNumber = invoiceNumber,
            Status = InvoiceStatus.Unpaid,
            Lines = lines,
            Subtotal = subtotal,
            Tax = tax,
            Total = total,
            DueDate = DateTime.UtcNow.AddDays(14),
            Notes = $"Invoice for Order #{order.Id}"
        };

        var created = await invoiceRepository.CreateAsync(invoice);
        return mapper.Map<InvoiceDto>(created);
    }

    public async Task<InvoiceDto?> MarkAsPaidAsync(string id)
    {
        var entity = await invoiceRepository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Status = InvoiceStatus.Paid;
        entity.PaidAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await invoiceRepository.UpdateAsync(entity);
        return mapper.Map<InvoiceDto>(entity);
    }
}
