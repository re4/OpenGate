namespace OpenGate.Domain.Entities;

public class ConfigurableOption
{
    public string Name { get; set; } = string.Empty;
    public string EnvironmentVariable { get; set; } = string.Empty;
    public List<ConfigurableOptionValue> Values { get; set; } = new();
}

public class ConfigurableOptionValue
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal PriceModifier { get; set; }
}
