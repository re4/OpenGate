using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class CategoryService(ICategoryRepository repository, IMapper mapper) : ICategoryService
{
    public async Task<CategoryDto?> GetByIdAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<CategoryDto>(entity);
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        var entities = await repository.GetAllAsync();
        return mapper.Map<IEnumerable<CategoryDto>>(entities);
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug)
    {
        var entity = await repository.GetBySlugAsync(slug);
        return entity == null ? null : mapper.Map<CategoryDto>(entity);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var entity = mapper.Map<Category>(dto);
        var created = await repository.CreateAsync(entity);
        return mapper.Map<CategoryDto>(created);
    }

    public async Task<CategoryDto?> UpdateAsync(string id, UpdateCategoryDto dto)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return null;

        mapper.Map(dto, entity);
        await repository.UpdateAsync(entity);
        return mapper.Map<CategoryDto>(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return false;

        await repository.DeleteAsync(id);
        return true;
    }
}
