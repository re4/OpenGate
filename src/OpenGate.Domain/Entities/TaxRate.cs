namespace OpenGate.Domain.Entities;

public class TaxRate : BaseEntity
{
    public string Country { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? StateName { get; set; }
    public decimal Rate { get; set; }
    public string Label { get; set; } = "Tax";
    public bool Inclusive { get; set; }
    public bool Enabled { get; set; } = true;
}
