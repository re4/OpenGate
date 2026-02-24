using System.Linq.Expressions;
using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> CreateAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(string id);
    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null);
}
