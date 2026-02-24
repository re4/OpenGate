namespace OpenGate.Extensions.Abstractions;

public interface IPaymentGateway : IOpenGateExtension
{
    Task<PaymentResult> CreatePaymentAsync(PaymentRequest request);
    Task<PaymentResult> VerifyPaymentAsync(string transactionId);
    Task<PaymentResult> RefundAsync(string transactionId, decimal amount);
    Task<string> GetPaymentUrl(PaymentRequest request);
    Task<WebhookResult> HandleWebhookAsync(string payload, Dictionary<string, string> headers);
}

public class PaymentRequest
{
    public string InvoiceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal AmountPaid { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class WebhookResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? InvoiceId { get; set; }
    public WebhookEventType EventType { get; set; }
    public decimal Amount { get; set; }
}

public enum WebhookEventType
{
    PaymentCompleted,
    PaymentFailed,
    PaymentRefunded,
    SubscriptionCancelled,
    Other
}
