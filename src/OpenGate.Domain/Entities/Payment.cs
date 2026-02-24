using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Entities;

public class Payment : BaseEntity
{
    public string InvoiceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? GatewayResponse { get; set; }
}
