using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Entities;

public class Order : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? ProvisioningId { get; set; } // external server ID
    public DateTime? NextDueDate { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public Dictionary<string, string> SelectedOptions { get; set; } = new();
}
