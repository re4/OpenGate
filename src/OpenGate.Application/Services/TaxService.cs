using Microsoft.AspNetCore.Identity;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class TaxService(ITaxRateRepository taxRateRepo, UserManager<ApplicationUser> userManager) : ITaxService
{
    public async Task<TaxLookupResult> GetTaxForUserAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return TaxLookupResult.None;

        return await GetTaxForLocationAsync(user.Country, user.State);
    }

    public async Task<TaxLookupResult> GetTaxForLocationAsync(string? countryCode, string? stateCode = null)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return TaxLookupResult.None;

        var rate = await taxRateRepo.GetByLocationAsync(countryCode, stateCode);
        if (rate == null || !rate.Enabled)
            return TaxLookupResult.None;

        return new TaxLookupResult
        {
            Rate = rate.Rate,
            Label = rate.Label,
            Inclusive = rate.Inclusive,
            Found = true,
            Country = rate.Country,
            State = rate.State
        };
    }
}
