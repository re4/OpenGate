using System.Linq.Expressions;
using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public abstract class MongoRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly IMongoCollection<T> Collection;

    protected MongoRepository(MongoDbContext context, IMongoCollection<T> collection)
    {
        Collection = collection;
    }

    public virtual async Task<T?> GetByIdAsync(string id)
    {
        return await Collection.Find(e => e.Id == id).FirstOrDefaultAsync();
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await Collection.Find(_ => true).ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await Collection.Find(predicate).ToListAsync();
    }

    public virtual async Task<T> CreateAsync(T entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await Collection.InsertOneAsync(entity);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(e => e.Id == entity.Id, entity);
    }

    public virtual async Task DeleteAsync(string id)
    {
        await Collection.DeleteOneAsync(e => e.Id == id);
    }

    public virtual async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await Collection.CountDocumentsAsync(_ => true)
            : await Collection.CountDocumentsAsync(predicate);
    }
}
