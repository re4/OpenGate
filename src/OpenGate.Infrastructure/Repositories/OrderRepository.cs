using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class OrderRepository(MongoDbContext context) : MongoRepository<Order>(context, context.Orders), IOrderRepository
{

    public async Task<IEnumerable<Order>> GetByUserAsync(string userId)
    {
        return await Collection.Find(o => o.UserId == userId).ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        return await Collection.Find(o => o.Status == status).ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOverdueOrdersAsync()
    {
        var now = DateTime.UtcNow;
        return await Collection.Find(o => o.Status == OrderStatus.Active && o.NextDueDate != null && o.NextDueDate < now).ToListAsync();
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null)
    {
        var filterBuilder = Builders<Order>.Filter;
        var filter = filterBuilder.Eq(o => o.Status, OrderStatus.Active);

        if (from.HasValue)
            filter = filter & filterBuilder.Gte(o => o.CreatedAt, from.Value);
        if (to.HasValue)
            filter = filter & filterBuilder.Lte(o => o.CreatedAt, to.Value);

        var orders = await Collection.Find(filter).ToListAsync();
        return orders.Sum(o => o.Total);
    }
}
