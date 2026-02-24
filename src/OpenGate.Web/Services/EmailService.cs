using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class EmailService(
    ISettingRepository settingRepo,
    IInvoiceRepository invoiceRepo,
    IOrderRepository orderRepo,
    ITicketRepository ticketRepo,
    UserManager<ApplicationUser> userManager,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var smtpHost = (await settingRepo.GetByKeyAsync("SmtpHost"))?.Value;
            var smtpPort = (await settingRepo.GetByKeyAsync("SmtpPort"))?.Value ?? "587";
            var smtpUser = (await settingRepo.GetByKeyAsync("SmtpUser"))?.Value;
            var smtpPass = (await settingRepo.GetByKeyAsync("SmtpPassword"))?.Value;
            var fromEmail = (await settingRepo.GetByKeyAsync("SmtpFrom"))?.Value ?? "noreply@opengate.local";
            var siteName = (await settingRepo.GetByKeyAsync("SiteName"))?.Value ?? "OpenGate";

            if (string.IsNullOrEmpty(smtpHost))
            {
                logger.LogWarning("SMTP not configured. Email to {To} not sent: {Subject}", to, subject);
                return;
            }

            using var client = new SmtpClient(smtpHost, int.Parse(smtpPort))
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, siteName),
                Subject = subject,
                Body = WrapInTemplate(htmlBody, siteName),
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message);
            logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }

    public async Task SendInvoiceNotificationAsync(string userId, string invoiceId)
    {
        var user = await userManager.FindByIdAsync(userId);
        var invoice = await invoiceRepo.GetByIdAsync(invoiceId);
        if (user?.Email == null || invoice == null) return;

        var subject = $"Invoice #{invoice.InvoiceNumber} - {invoice.Total:C}";
        var body = $@"
            <h2 style='margin:0 0 16px;font-size:18px;font-weight:600;'>New Invoice</h2>
            <p style='color:#a1a1aa;'>Hi {user.FirstName},</p>
            <p style='color:#a1a1aa;'>A new invoice has been generated.</p>
            <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
                <tr><td style='padding:8px 0;border-bottom:1px solid #2e2e33;color:#a1a1aa;'>Invoice #</td><td style='padding:8px 0;border-bottom:1px solid #2e2e33;font-family:monospace;'>{invoice.InvoiceNumber}</td></tr>
                <tr><td style='padding:8px 0;border-bottom:1px solid #2e2e33;color:#a1a1aa;'>Amount</td><td style='padding:8px 0;border-bottom:1px solid #2e2e33;font-family:monospace;'>{invoice.Total:F2}</td></tr>
                <tr><td style='padding:8px 0;color:#a1a1aa;'>Due</td><td style='padding:8px 0;'>{invoice.DueDate:MMM dd, yyyy}</td></tr>
            </table>
            <p style='color:#a1a1aa;'>Log in to view and pay this invoice.</p>";

        await SendAsync(user.Email, subject, body);
    }

    public async Task SendOrderStatusChangeAsync(string userId, string orderId, string newStatus)
    {
        var user = await userManager.FindByIdAsync(userId);
        var order = await orderRepo.GetByIdAsync(orderId);
        if (user?.Email == null || order == null) return;

        var subject = $"Order Update - Status: {newStatus}";
        var body = $@"
            <h2 style='margin:0 0 16px;font-size:18px;font-weight:600;'>Order Update</h2>
            <p style='color:#a1a1aa;'>Hi {user.FirstName},</p>
            <p style='color:#a1a1aa;'>Your order status has changed.</p>
            <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
                <tr><td style='padding:8px 0;border-bottom:1px solid #2e2e33;color:#a1a1aa;'>Order</td><td style='padding:8px 0;border-bottom:1px solid #2e2e33;font-family:monospace;'>{order.Id[..8]}</td></tr>
                <tr><td style='padding:8px 0;color:#a1a1aa;'>Status</td><td style='padding:8px 0;font-weight:600;'>{newStatus}</td></tr>
            </table>
            <p style='color:#a1a1aa;'>Log in for details.</p>";

        await SendAsync(user.Email, subject, body);
    }

    public async Task SendTicketReplyNotificationAsync(string userId, string ticketId)
    {
        var user = await userManager.FindByIdAsync(userId);
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (user?.Email == null || ticket == null) return;

        var subject = $"Ticket Reply - {ticket.Subject}";
        var body = $@"
            <h2 style='margin:0 0 16px;font-size:18px;font-weight:600;'>Ticket Reply</h2>
            <p style='color:#a1a1aa;'>Hi {user.FirstName},</p>
            <p style='color:#a1a1aa;'>New reply on: <strong style='color:#e4e4e7;'>{ticket.Subject}</strong></p>
            <p style='color:#a1a1aa;'>Log in to view the response.</p>";

        await SendAsync(user.Email, subject, body);
    }

    public async Task SendWelcomeEmailAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user?.Email == null) return;

        var siteName = (await settingRepo.GetByKeyAsync("SiteName"))?.Value ?? "OpenGate";

        var subject = $"Welcome to {siteName}!";
        var body = $@"
            <h2 style='margin:0 0 16px;font-size:18px;font-weight:600;'>Welcome</h2>
            <p style='color:#a1a1aa;'>Hi {user.FirstName},</p>
            <p style='color:#a1a1aa;'>Your account is ready. You can browse products and place orders.</p>
            <p style='color:#a1a1aa;'>Open a support ticket if you need help.</p>";

        await SendAsync(user.Email, subject, body);
    }

    private static string WrapInTemplate(string content, string siteName)
    {
        return $@"
        <!DOCTYPE html>
        <html>
        <head><meta charset='utf-8'></head>
        <body style='font-family:Inter,-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;margin:0;padding:0;background:#0e0e10;color:#e4e4e7;'>
            <div style='max-width:560px;margin:0 auto;'>
                <div style='padding:32px 0 16px;text-align:left;'>
                    <span style='font-size:16px;font-weight:600;color:#e4e4e7;'>{siteName}</span>
                </div>
                <div style='background:#18181b;border:1px solid #2e2e33;border-radius:8px;padding:28px;'>
                    {content}
                </div>
                <div style='padding:16px 0;text-align:center;'>
                    <p style='margin:0;color:#71717a;font-size:11px;'>{siteName}</p>
                </div>
            </div>
        </body>
        </html>";
    }
}
