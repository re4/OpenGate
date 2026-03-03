using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class ThemeService(IThemeRepository repository, IThemeCssProvider cssProvider, IMapper mapper) : IThemeService
{
    public async Task<IEnumerable<ThemeDto>> GetAllAsync()
    {
        var entities = await repository.GetAllAsync();
        return mapper.Map<IEnumerable<ThemeDto>>(entities);
    }

    public async Task<ThemeDto?> GetByIdAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<ThemeDto>(entity);
    }

    public async Task<ThemeDto?> GetActiveAsync()
    {
        var entity = await repository.GetActiveAsync();
        return entity == null ? null : mapper.Map<ThemeDto>(entity);
    }

    public async Task<ThemeDto> CreateAsync(CreateThemeDto dto)
    {
        var entity = mapper.Map<Theme>(dto);
        entity.IsPreset = false;
        await repository.CreateAsync(entity);
        return mapper.Map<ThemeDto>(entity);
    }

    public async Task UpdateAsync(string id, UpdateThemeDto dto)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) throw new InvalidOperationException("Theme not found.");

        entity.Name = dto.Name;
        entity.Variables = dto.Variables;
        entity.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(entity);

        if (entity.IsActive)
            cssProvider.InvalidateCache();
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return;
        if (entity.IsPreset) throw new InvalidOperationException("Cannot delete a preset theme.");

        bool wasActive = entity.IsActive;
        await repository.DeleteAsync(id);

        if (wasActive)
            cssProvider.InvalidateCache();
    }

    public async Task SetActiveAsync(string themeId)
    {
        await repository.SetActiveAsync(themeId);
        cssProvider.InvalidateCache();
    }
}
