using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class SettingRepository(MongoDbContext context) : MongoRepository<Setting>(context, context.Settings), ISettingRepository
{

    public async Task<Setting?> GetByKeyAsync(string key)
    {
        return await Collection.Find(s => s.Key == key).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Setting>> GetByGroupAsync(string group)
    {
        return await Collection.Find(s => s.Group == group).ToListAsync();
    }

    public async Task SetValueAsync(string key, string value)
    {
        var existing = await GetByKeyAsync(key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            await Collection.ReplaceOneAsync(s => s.Id == existing.Id, existing);
        }
        else
        {
            var setting = new Setting
            {
                Key = key,
                Value = value,
                Group = "General"
            };
            await CreateAsync(setting);
        }
    }
}
