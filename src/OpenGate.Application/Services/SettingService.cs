using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class SettingService(ISettingRepository repository, IMapper mapper) : ISettingService
{
    public async Task<IEnumerable<SettingDto>> GetAllAsync()
    {
        var entities = await repository.GetAllAsync();
        return mapper.Map<IEnumerable<SettingDto>>(entities);
    }

    public async Task<SettingDto?> GetByKeyAsync(string key)
    {
        var entity = await repository.GetByKeyAsync(key);
        return entity == null ? null : mapper.Map<SettingDto>(entity);
    }

    public async Task<IEnumerable<SettingDto>> GetByGroupAsync(string group)
    {
        var entities = await repository.GetByGroupAsync(group);
        return mapper.Map<IEnumerable<SettingDto>>(entities);
    }

    public async Task SetValueAsync(string key, string value)
    {
        await repository.SetValueAsync(key, value);
    }
}
