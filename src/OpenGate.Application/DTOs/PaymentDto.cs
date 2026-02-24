using OpenGate.Domain.Enums;

namespace OpenGate.Application.DTOs;

public class PaymentDto
{
    public string Id { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; }
    public string? GatewayResponse { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreatePaymentDto
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
