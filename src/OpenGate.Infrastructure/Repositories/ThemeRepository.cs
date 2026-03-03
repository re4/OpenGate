using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class ThemeRepository(MongoDbContext context) : MongoRepository<Theme>(context, context.Themes), IThemeRepository
{
    public async Task<Theme?> GetActiveAsync()
    {
        return await Collection.Find(t => t.IsActive).FirstOrDefaultAsync();
    }

    public async Task SetActiveAsync(string themeId)
    {
        var updateAll = Builders<Theme>.Update
            .Set(t => t.IsActive, false)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);
        await Collection.UpdateManyAsync(_ => true, updateAll);

        var activateOne = Builders<Theme>.Update
            .Set(t => t.IsActive, true)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);
        await Collection.UpdateOneAsync(t => t.Id == themeId, activateOne);
    }
}
