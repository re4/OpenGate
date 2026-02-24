using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SetupFee { get; set; }
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public int Stock { get; set; } = -1; // -1 = unlimited
    public bool IsActive { get; set; } = true;
    public string? ServerId { get; set; } // provisioning server reference
    public List<ConfigurableOption> ConfigurableOptions { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}
