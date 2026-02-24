using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class ExtensionConfigRepository(MongoDbContext context) : MongoRepository<ExtensionConfig>(context, context.ExtensionConfigs), IExtensionConfigRepository
{

    public async Task<ExtensionConfig?> GetByNameAsync(string name)
    {
        return await Collection.Find(e => e.Name == name).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ExtensionConfig>> GetEnabledAsync()
    {
        return await Collection.Find(e => e.IsEnabled).ToListAsync();
    }

    public async Task<IEnumerable<ExtensionConfig>> GetByTypeAsync(ExtensionType type)
    {
        return await Collection.Find(e => e.Type == type).ToListAsync();
    }
}
