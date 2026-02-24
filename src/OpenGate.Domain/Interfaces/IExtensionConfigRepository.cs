using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Interfaces;

public interface IExtensionConfigRepository : IRepository<ExtensionConfig>
{
    Task<ExtensionConfig?> GetByNameAsync(string name);
    Task<IEnumerable<ExtensionConfig>> GetEnabledAsync();
    Task<IEnumerable<ExtensionConfig>> GetByTypeAsync(ExtensionType type);
}
