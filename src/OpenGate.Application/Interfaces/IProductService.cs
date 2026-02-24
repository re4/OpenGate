using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(string id);
    Task<IEnumerable<ProductDto>> GetAllAsync();
    Task<IEnumerable<ProductDto>> GetByCategoryAsync(string categoryId);
    Task<IEnumerable<ProductDto>> GetActiveAsync();
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto?> UpdateAsync(string id, UpdateProductDto dto);
    Task<bool> DeleteAsync(string id);
}
