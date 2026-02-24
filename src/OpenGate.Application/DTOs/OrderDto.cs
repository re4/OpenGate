using OpenGate.Domain.Enums;

namespace OpenGate.Application.DTOs;

public class OrderDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? ProvisioningId { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrderItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public Dictionary<string, string> SelectedOptions { get; set; } = new();
}

public class CartItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public Dictionary<string, string> SelectedOptions { get; set; } = new();
}

public class CreateOrderDto
{
    public string UserId { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}
