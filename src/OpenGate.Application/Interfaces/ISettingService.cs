using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface ISettingService
{
    Task<IEnumerable<SettingDto>> GetAllAsync();
    Task<SettingDto?> GetByKeyAsync(string key);
    Task<IEnumerable<SettingDto>> GetByGroupAsync(string group);
    Task SetValueAsync(string key, string value);
}
