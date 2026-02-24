using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<IEnumerable<Category>> GetActiveCategoriesAsync();
    Task<Category?> GetBySlugAsync(string slug);
}
