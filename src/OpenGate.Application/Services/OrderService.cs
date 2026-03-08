using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class OrderService(IOrderRepository orderRepository, IProductRepository productRepository, ISettingRepository settingRepository, ITaxService taxService, IMapper mapper) : IOrderService
{
    public async Task<OrderDto?> GetByIdAsync(string id)
    {
        var entity = await orderRepository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<OrderDto>(entity);
    }

    public async Task<IEnumerable<OrderDto>> GetAllAsync()
    {
        var entities = await orderRepository.GetAllAsync();
        return mapper.Map<IEnumerable<OrderDto>>(entities);
    }

    public async Task<IEnumerable<OrderDto>> GetByUserAsync(string userId)
    {
        var entities = await orderRepository.GetByUserAsync(userId);
        return mapper.Map<IEnumerable<OrderDto>>(entities);
    }

    public async Task<IEnumerable<OrderDto>> GetByStatusAsync(OrderStatus status)
    {
        var entities = await orderRepository.GetByStatusAsync(status);
        return mapper.Map<IEnumerable<OrderDto>>(entities);
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto)
    {
        var order = await CreateOrderFromItemsAsync(dto.UserId, dto.Items, dto.Notes);
        return mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto> CreateFromCartAsync(string userId, List<CartItemDto> items, string? notes = null)
    {
        var order = await CreateOrderFromItemsAsync(userId, items, notes);
        return mapper.Map<OrderDto>(order);
    }

    private async Task<Order> CreateOrderFromItemsAsync(string userId, List<CartItemDto> items, string? notes)
    {
        var currency = (await settingRepository.GetByKeyAsync("Currency"))?.Value ?? "USD";
        var taxLookup = await taxService.GetTaxForUserAsync(userId);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0;

        foreach (var item in items)
        {
            var product = await productRepository.GetByIdAsync(item.ProductId);
            if (product == null || !product.IsActive) continue;

            var unitPrice = item.UnitPrice > 0 ? item.UnitPrice : product.Price;
            var itemTotal = unitPrice * item.Quantity;
            subtotal += itemTotal;

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = !string.IsNullOrEmpty(item.ProductName) ? item.ProductName : product.Name,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                Total = itemTotal,
                BillingCycle = item.BillingCycle,
                SelectedOptions = item.SelectedOptions
            });
        }

        var tax = taxLookup.CalculateTax(subtotal);
        var total = taxLookup.CalculateTotal(subtotal);

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            Items = orderItems,
            Subtotal = subtotal,
            Tax = tax,
            Total = total,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim(),
            Notes = notes
        };

        return await orderRepository.CreateAsync(order);
    }

    public async Task<OrderDto?> UpdateAsync(string id, CreateOrderDto dto)
    {
        var entity = await orderRepository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        await orderRepository.UpdateAsync(entity);
        return mapper.Map<OrderDto>(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await orderRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await orderRepository.DeleteAsync(id);
        return true;
    }

    public async Task<OrderDto?> SuspendOrderAsync(string id)
    {
        var entity = await orderRepository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Status = OrderStatus.Suspended;
        entity.SuspendedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await orderRepository.UpdateAsync(entity);
        return mapper.Map<OrderDto>(entity);
    }

    public async Task<OrderDto?> ActivateOrderAsync(string id)
    {
        var entity = await orderRepository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Status = OrderStatus.Active;
        entity.SuspendedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await orderRepository.UpdateAsync(entity);
        return mapper.Map<OrderDto>(entity);
    }

    public async Task<OrderDto?> CancelOrderAsync(string id)
    {
        var entity = await orderRepository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Status = OrderStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await orderRepository.UpdateAsync(entity);
        return mapper.Map<OrderDto>(entity);
    }
}
