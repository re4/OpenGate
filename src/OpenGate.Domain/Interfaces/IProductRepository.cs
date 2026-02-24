using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(string categoryId);
    Task<IEnumerable<Product>> GetActiveProductsAsync();
    Task DecrementStockAsync(string productId, int quantity = 1);
}
