using OpenGate.Domain.Entities;

namespace OpenGate.Domain.Interfaces;

public interface ITaxRateRepository : IRepository<TaxRate>
{
    Task<TaxRate?> GetByLocationAsync(string countryCode, string? stateCode = null);
    Task<IEnumerable<TaxRate>> GetByCountryAsync(string countryCode);
}
