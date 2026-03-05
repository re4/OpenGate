using OpenGate.Domain.Enums;

namespace OpenGate.Application.DTOs;

public class InvoiceDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public List<InvoiceLineDto> Lines { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public string TaxLabel { get; set; } = "Tax";
    public decimal TaxRate { get; set; }
    public bool TaxInclusive { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class CreateInvoiceDto
{
    public string UserId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public List<InvoiceLineDto> Lines { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
}
