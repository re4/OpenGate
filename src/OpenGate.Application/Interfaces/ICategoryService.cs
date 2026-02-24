using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface ICategoryService
{
    Task<CategoryDto?> GetByIdAsync(string id);
    Task<IEnumerable<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetBySlugAsync(string slug);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto?> UpdateAsync(string id, UpdateCategoryDto dto);
    Task<bool> DeleteAsync(string id);
}
