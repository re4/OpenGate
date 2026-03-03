using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface IThemeService
{
    Task<IEnumerable<ThemeDto>> GetAllAsync();
    Task<ThemeDto?> GetByIdAsync(string id);
    Task<ThemeDto?> GetActiveAsync();
    Task<ThemeDto> CreateAsync(CreateThemeDto dto);
    Task UpdateAsync(string id, UpdateThemeDto dto);
    Task DeleteAsync(string id);
    Task SetActiveAsync(string themeId);
}
