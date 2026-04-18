using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Web.Controllers;

/// <summary>
/// Handles inbound webhook callbacks from configured payment gateways.
/// Verification of signatures is delegated to the individual gateway
/// implementations; this controller is responsible for safely projecting
/// verified results onto invoices, payments and orders in an idempotent way.
/// </summary>
[Route("api/webhooks")]
[ApiController]
[EnableRateLimiting("webhook")]
public class WebhookController(
    IExtensionConfigRepository extensionRepo,
    IPaymentRepository paymentRepo,
    IInvoiceRepository invoiceRepo,
    IOrderRepository orderRepo,
    IServiceProvider serviceProvider,
    ILogger<WebhookController> logger) : ControllerBase
{
    /// <summary>
    /// Receives a webhook payload for the given gateway, verifies it via the
    /// gateway implementation, and applies an idempotent state transition.
    /// Always returns 200 once the payload is verified so providers do not
    /// retry needlessly; verification failures still return 4xx.
    /// </summary>
    [HttpPost("{gatewayName}")]
    public async Task<IActionResult> HandleWebhook(string gatewayName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gatewayName) || gatewayName.Length > 64)
                return BadRequest();

            var config = await extensionRepo.GetByNameAsync(gatewayName);
            if (config is not { IsEnabled: true })
                return NotFound();

            var gateway = ResolveGateway(gatewayName);
            if (gateway == null)
                return NotFound();

            await gateway.InitializeAsync(config.Settings);

            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            var result = await gateway.HandleWebhookAsync(payload, headers);

            if (!result.Success)
            {
                logger.LogWarning("Rejected webhook from {Gateway}: signature/payload validation failed", gatewayName);
                return BadRequest();
            }

            if (!string.IsNullOrEmpty(result.InvoiceId))
            {
                await ProcessWebhookResult(gatewayName, result);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook processing failed for {Gateway}", gatewayName);
            return StatusCode(500);
        }
    }

    private IPaymentGateway? ResolveGateway(string name)
    {
        var gateways = serviceProvider.GetServices<IPaymentGateway>();
        return gateways.FirstOrDefault(g =>
            g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Applies the verified webhook result to invoices/orders/payments while
    /// remaining idempotent against duplicate deliveries by checking the
    /// provider transaction id and current invoice status.
    /// </summary>
    private async Task ProcessWebhookResult(string gatewayName, WebhookResult result)
    {
        switch (result.EventType)
        {
            case WebhookEventType.PaymentCompleted:
            {
                var invoice = await invoiceRepo.GetByIdAsync(result.InvoiceId!);
                if (invoice == null)
                {
                    logger.LogWarning("Webhook {Gateway} referenced unknown invoice {InvoiceId}", gatewayName, result.InvoiceId);
                    return;
                }

                if (!string.IsNullOrEmpty(result.TransactionId))
                {
                    var existingPayment = await paymentRepo.GetByTransactionIdAsync(result.TransactionId);
                    if (existingPayment != null)
                    {
                        logger.LogInformation(
                            "Duplicate webhook for transaction {TransactionId} ignored",
                            result.TransactionId);
                        return;
                    }
                }

                if (invoice.Status == InvoiceStatus.Paid)
                {
                    logger.LogInformation("Invoice {InvoiceId} already paid; ignoring duplicate", invoice.Id);
                    return;
                }

                try
                {
                    await paymentRepo.CreateAsync(new Payment
                    {
                        InvoiceId = invoice.Id,
                        UserId = invoice.UserId,
                        Gateway = gatewayName,
                        TransactionId = result.TransactionId,
                        Amount = result.Amount,
                        Currency = invoice.Currency,
                        Status = PaymentStatus.Completed
                    });
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    logger.LogInformation("Duplicate payment for transaction {TransactionId} suppressed by unique index", result.TransactionId);
                    return;
                }

                invoice.Status = InvoiceStatus.Paid;
                invoice.PaidAt = DateTime.UtcNow;
                await invoiceRepo.UpdateAsync(invoice);

                if (!string.IsNullOrEmpty(invoice.OrderId))
                {
                    var order = await orderRepo.GetByIdAsync(invoice.OrderId);
                    if (order != null && order.Status == OrderStatus.Pending)
                    {
                        order.Status = OrderStatus.Active;
                        await orderRepo.UpdateAsync(order);
                    }
                }
                break;
            }

            case WebhookEventType.PaymentFailed:
                logger.LogWarning("Payment failed for invoice {InvoiceId} via {Gateway}", result.InvoiceId, gatewayName);
                break;

            case WebhookEventType.PaymentRefunded:
            {
                var refundInvoice = await invoiceRepo.GetByIdAsync(result.InvoiceId!);
                if (refundInvoice != null && refundInvoice.Status != InvoiceStatus.Refunded)
                {
                    refundInvoice.Status = InvoiceStatus.Refunded;
                    await invoiceRepo.UpdateAsync(refundInvoice);
                }
                break;
            }
        }
    }
}
