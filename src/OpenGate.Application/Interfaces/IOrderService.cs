using OpenGate.Application.DTOs;
using OpenGate.Domain.Enums;

namespace OpenGate.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto?> GetByIdAsync(string id);
    Task<IEnumerable<OrderDto>> GetAllAsync();
    Task<IEnumerable<OrderDto>> GetByUserAsync(string userId);
    Task<IEnumerable<OrderDto>> GetByStatusAsync(OrderStatus status);
    Task<OrderDto> CreateAsync(CreateOrderDto dto);
    Task<OrderDto> CreateFromCartAsync(string userId, List<CartItemDto> items, string? notes = null);
    Task<OrderDto?> UpdateAsync(string id, CreateOrderDto dto);
    Task<bool> DeleteAsync(string id);
    Task<OrderDto?> SuspendOrderAsync(string id);
    Task<OrderDto?> ActivateOrderAsync(string id);
    Task<OrderDto?> CancelOrderAsync(string id);
}
