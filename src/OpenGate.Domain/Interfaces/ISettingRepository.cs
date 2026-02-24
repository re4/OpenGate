using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface ISettingRepository : IRepository<Setting>
{
    Task<Setting?> GetByKeyAsync(string key);
    Task<IEnumerable<Setting>> GetByGroupAsync(string group);
    Task SetValueAsync(string key, string value);
}
