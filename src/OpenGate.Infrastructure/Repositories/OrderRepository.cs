using MongoDB.Bson;
using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

/// <summary>
/// MongoDB-backed repository for <see cref="Order"/>. Aggregations and
/// statistics are pushed into the database so dashboards and reports do not
/// stream entire collections through the application.
/// </summary>
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

    /// <summary>
    /// Sums <see cref="Order.Total"/> for active orders inside an optional
    /// window. Uses a server-side aggregation so memory stays constant.
    /// </summary>
    public async Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null)
    {
        var matchClauses = new List<BsonDocument>
        {
            new("Status", (int)OrderStatus.Active)
        };

        if (from.HasValue || to.HasValue)
        {
            var range = new BsonDocument();
            if (from.HasValue) range["$gte"] = from.Value;
            if (to.HasValue) range["$lte"] = to.Value;
            matchClauses.Add(new BsonDocument("CreatedAt", range));
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("$and", new BsonArray(matchClauses))),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = BsonNull.Value,
                ["total"] = new BsonDocument("$sum", "$Total")
            })
        };

        var result = await Collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
        if (result == null) return 0m;
        return result.TryGetValue("total", out var totalValue) ? totalValue.ToDecimal() : 0m;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetRecentAsync(int limit)
    {
        if (limit <= 0) return Array.Empty<Order>();
        return await Collection.Find(_ => true)
            .Sort(Builders<Order>.Sort.Descending(o => o.CreatedAt))
            .Limit(limit)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<OrderStatus, long>> GetStatusCountsAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$Status",
                ["count"] = new BsonDocument("$sum", 1)
            })
        };

        var results = await Collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        var dict = new Dictionary<OrderStatus, long>();
        foreach (var doc in results)
        {
            if (!doc.TryGetValue("_id", out var key) || !doc.TryGetValue("count", out var count))
                continue;
            if (!key.IsInt32 && !key.IsInt64) continue;
            dict[(OrderStatus)key.ToInt32()] = count.ToInt64();
        }
        return dict;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(int Month, decimal Total)>> GetMonthlyRevenueAsync(int year)
    {
        var startOfYear = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextYear = startOfYear.AddYears(1);

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                ["Status"] = (int)OrderStatus.Active,
                ["CreatedAt"] = new BsonDocument
                {
                    ["$gte"] = startOfYear,
                    ["$lt"] = startOfNextYear
                }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument("$month", "$CreatedAt"),
                ["total"] = new BsonDocument("$sum", "$Total")
            })
        };

        var docs = await Collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        var lookup = docs.ToDictionary(d => d["_id"].ToInt32(), d => d.TryGetValue("total", out var t) ? t.ToDecimal() : 0m);

        var result = new List<(int Month, decimal Total)>(12);
        for (var month = 1; month <= 12; month++)
        {
            result.Add((month, lookup.TryGetValue(month, out var amount) ? amount : 0m));
        }
        return result;
    }
}
