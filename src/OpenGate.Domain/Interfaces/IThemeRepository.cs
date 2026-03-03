using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface IThemeRepository : IRepository<Theme>
{
    Task<Theme?> GetActiveAsync();
    Task SetActiveAsync(string themeId);
}
