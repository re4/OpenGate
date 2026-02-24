using OpenGate.Domain.Enums;

namespace OpenGate.Application.DTOs;

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SetupFee { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }
    public string? ServerId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<ConfigurableOptionDto> ConfigurableOptions { get; set; } = new();
}

public class ConfigurableOptionValueDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal PriceModifier { get; set; }
}

public class ConfigurableOptionDto
{
    public string Name { get; set; } = string.Empty;
    public string EnvironmentVariable { get; set; } = string.Empty;
    public List<ConfigurableOptionValueDto> Values { get; set; } = new();
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SetupFee { get; set; }
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public int Stock { get; set; } = -1;
    public bool IsActive { get; set; } = true;
    public string? ServerId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<ConfigurableOptionDto> ConfigurableOptions { get; set; } = new();
}

public class UpdateProductDto : CreateProductDto
{
}
