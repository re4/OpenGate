using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByUserAsync(string userId);
    Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
    Task<IEnumerable<Order>> GetOverdueOrdersAsync();
    Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null);
}
