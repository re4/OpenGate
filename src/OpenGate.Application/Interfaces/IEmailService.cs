namespace OpenGate.Application.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
    Task SendInvoiceNotificationAsync(string userId, string invoiceId);
    Task SendOrderStatusChangeAsync(string userId, string orderId, string newStatus);
    Task SendTicketReplyNotificationAsync(string userId, string ticketId);
    Task SendWelcomeEmailAsync(string userId);
}
