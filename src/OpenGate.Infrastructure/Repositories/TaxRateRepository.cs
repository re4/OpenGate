using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class TaxRateRepository(MongoDbContext context) : MongoRepository<TaxRate>(context, context.TaxRates), ITaxRateRepository
{
    public async Task<TaxRate?> GetByLocationAsync(string countryCode, string? stateCode = null)
    {
        countryCode = countryCode.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(stateCode))
        {
            stateCode = stateCode.Trim().ToUpperInvariant();
            var stateRate = await Collection.Find(t =>
                t.Country == countryCode && t.State == stateCode && t.Enabled).FirstOrDefaultAsync();
            if (stateRate != null) return stateRate;
        }

        return await Collection.Find(t =>
            t.Country == countryCode && t.State == null && t.Enabled).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TaxRate>> GetByCountryAsync(string countryCode)
    {
        countryCode = countryCode.Trim().ToUpperInvariant();
        return await Collection.Find(t => t.Country == countryCode).SortBy(t => t.State).ToListAsync();
    }
}
