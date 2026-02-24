using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class ProductRepository(MongoDbContext context) : MongoRepository<Product>(context, context.Products), IProductRepository
{

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string categoryId)
    {
        return await Collection.Find(p => p.CategoryId == categoryId).ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        return await Collection.Find(p => p.IsActive).ToListAsync();
    }

    public async Task DecrementStockAsync(string productId, int quantity = 1)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, productId),
            Builders<Product>.Filter.Gte(p => p.Stock, quantity),
            Builders<Product>.Filter.Ne(p => p.Stock, -1));
        var update = Builders<Product>.Update
            .Inc(p => p.Stock, -quantity)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await Collection.UpdateOneAsync(filter, update);
    }
}
