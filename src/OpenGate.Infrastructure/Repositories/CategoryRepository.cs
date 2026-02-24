using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class CategoryRepository(MongoDbContext context) : MongoRepository<Category>(context, context.Categories), ICategoryRepository
{

    public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
    {
        return await Collection.Find(c => c.IsActive).ToListAsync();
    }

    public async Task<Category?> GetBySlugAsync(string slug)
    {
        return await Collection.Find(c => c.Slug == slug).FirstOrDefaultAsync();
    }
}
