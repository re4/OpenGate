namespace OpenGate.Application.Migration;

public class PmUser
{
    public long Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public long? RoleId { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmCategory
{
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public long? ParentId { get; set; }
    public int? Sort { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmProduct
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public int? Stock { get; set; }
    public long? ServerId { get; set; }
    public int? Sort { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmPlan
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string PriceableType { get; set; } = string.Empty;
    public long PriceableId { get; set; }
    public string Type { get; set; } = string.Empty; // free, one-time, recurring
    public int? BillingPeriod { get; set; }
    public string? BillingUnit { get; set; } // hour, day, week, month, year
    public int? Sort { get; set; }
}

public class PmPrice
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SetupFee { get; set; }
}

public class PmOrder
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmService
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? OrderId { get; set; }
    public long? ProductId { get; set; }
    public long UserId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
    public long? PlanId { get; set; }
    public long? CouponId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? SubscriptionId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmInvoice
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string? Number { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmInvoiceItem
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmInvoiceTransaction
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public long? GatewayId { get; set; }
    public decimal Amount { get; set; }
    public decimal? Fee { get; set; }
    public string? TransactionId { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmTicket
{
    public long Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Department { get; set; }
    public long UserId { get; set; }
    public long? AssignedTo { get; set; }
    public long? ServiceId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmTicketMessage
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public long UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmExtension
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public bool Enabled { get; set; }
}

public class PmConfigOption
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Sort { get; set; }
    public bool Hidden { get; set; }
    public string? Description { get; set; }
}

public class PmConfigOptionProduct
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long ConfigOptionId { get; set; }
}

public class PmServiceConfig
{
    public long Id { get; set; }
    public long? ConfigOptionId { get; set; }
    public long? ConfigValueId { get; set; }
    public string ConfigurableType { get; set; } = string.Empty;
    public long ConfigurableId { get; set; }
}
