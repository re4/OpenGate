using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Entities;

public class Ticket : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public string? AssignedTo { get; set; }
    public string? OrderId { get; set; }
    public List<TicketMessage> Messages { get; set; } = new();
    public DateTime? ClosedAt { get; set; }
}

public class TicketMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public bool IsStaff { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<TicketAttachment> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TicketAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Size { get; set; }
}
