using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class ProductService(IProductRepository repository, IMapper mapper) : IProductService
{
    public async Task<ProductDto?> GetByIdAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<ProductDto>(entity);
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
    {
        var entities = await repository.GetAllAsync();
        return mapper.Map<IEnumerable<ProductDto>>(entities);
    }

    public async Task<IEnumerable<ProductDto>> GetByCategoryAsync(string categoryId)
    {
        var entities = await repository.GetByCategoryAsync(categoryId);
        return mapper.Map<IEnumerable<ProductDto>>(entities);
    }

    public async Task<IEnumerable<ProductDto>> GetActiveAsync()
    {
        var entities = await repository.GetActiveProductsAsync();
        return mapper.Map<IEnumerable<ProductDto>>(entities);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var entity = mapper.Map<Product>(dto);
        var created = await repository.CreateAsync(entity);
        return mapper.Map<ProductDto>(created);
    }

    public async Task<ProductDto?> UpdateAsync(string id, UpdateProductDto dto)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return null;

        mapper.Map(dto, entity);
        await repository.UpdateAsync(entity);
        return mapper.Map<ProductDto>(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return false;

        await repository.DeleteAsync(id);
        return true;
    }
}
