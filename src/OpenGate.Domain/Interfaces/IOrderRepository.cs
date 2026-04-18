using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="Order"/> aggregate.
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByUserAsync(string userId);
    Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
    Task<IEnumerable<Order>> GetOverdueOrdersAsync();

    /// <summary>
    /// Returns the sum of <see cref="Order.Total"/> for active orders within
    /// the supplied window. Implemented in the database to avoid loading
    /// rows into memory.
    /// </summary>
    Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Returns the most recent N orders ordered by creation time.
    /// </summary>
    Task<IReadOnlyList<Order>> GetRecentAsync(int limit);

    /// <summary>
    /// Counts orders by status. Implemented as a single grouped query so
    /// callers do not issue N round-trips to obtain status totals.
    /// </summary>
    Task<IReadOnlyDictionary<OrderStatus, long>> GetStatusCountsAsync();

    /// <summary>
    /// Returns total revenue for active orders bucketed by month for the
    /// given calendar year.
    /// </summary>
    Task<IReadOnlyList<(int Month, decimal Total)>> GetMonthlyRevenueAsync(int year);
}
