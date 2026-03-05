using OpenGate.Domain.Enums;

namespace OpenGate.Application.DTOs;

public class TicketDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public string? AssignedTo { get; set; }
    public string? OrderId { get; set; }
    public List<TicketMessageDto> Messages { get; set; } = new();
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TicketMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public bool IsStaff { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<TicketAttachmentDto> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class TicketAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Size { get; set; }
}

public class CreateTicketDto
{
    public string UserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public string? OrderId { get; set; }
    public string Body { get; set; } = string.Empty;
}

public class CreateTicketMessageDto
{
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public bool IsStaff { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<TicketAttachmentDto> Attachments { get; set; } = new();
}
