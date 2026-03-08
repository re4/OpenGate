using OpenGate.Domain.Entities;

namespace OpenGate.Application.Interfaces;

public class TaxLookupResult
{
    public decimal Rate { get; set; }
    public string Label { get; set; } = "Tax";
    public bool Inclusive { get; set; }
    public bool Found { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }

    public decimal CalculateTax(decimal subtotal)
    {
        if (Rate <= 0) return 0;
        if (Inclusive) return Math.Round(subtotal - (subtotal / (1 + Rate / 100)), 2);
        return Math.Round(subtotal * Rate / 100, 2);
    }

    public decimal CalculateTotal(decimal subtotal)
    {
        if (Rate <= 0) return subtotal;
        if (Inclusive) return subtotal;
        return subtotal + Math.Round(subtotal * Rate / 100, 2);
    }

    public string TaxDisplay => Rate > 0 ? $"{Label} ({Rate:G}%)" : Label;

    public static TaxLookupResult None => new() { Found = false };
}

public interface ITaxService
{
    Task<TaxLookupResult> GetTaxForUserAsync(string userId);
    Task<TaxLookupResult> GetTaxForLocationAsync(string? countryCode, string? stateCode = null);
}
