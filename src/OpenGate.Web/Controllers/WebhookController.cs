using Microsoft.AspNetCore.Mvc;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Extensions.Abstractions;

namespace OpenGate.Web.Controllers;

[Route("api/webhooks")]
[ApiController]
public class WebhookController(
    IExtensionConfigRepository extensionRepo,
    IPaymentRepository paymentRepo,
    IInvoiceRepository invoiceRepo,
    IOrderRepository orderRepo,
    IServiceProvider serviceProvider,
    ILogger<WebhookController> logger) : ControllerBase
{
    [HttpPost("{gatewayName}")]
    public async Task<IActionResult> HandleWebhook(string gatewayName)
    {
        try
        {
            var config = await extensionRepo.GetByNameAsync(gatewayName);
            if (config == null || !config.IsEnabled)
                return NotFound("Gateway not found or disabled");

            var gateway = ResolveGateway(gatewayName);
            if (gateway == null)
                return BadRequest("Gateway not registered");

            await gateway.InitializeAsync(config.Settings);

            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();

            var headers = Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            var result = await gateway.HandleWebhookAsync(payload, headers);

            if (result.Success && !string.IsNullOrEmpty(result.InvoiceId))
            {
                await ProcessWebhookResult(result);
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

    private async Task ProcessWebhookResult(WebhookResult result)
    {
        switch (result.EventType)
        {
            case WebhookEventType.PaymentCompleted:
                var invoice = await invoiceRepo.GetByIdAsync(result.InvoiceId!);
                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.Paid;
                    invoice.PaidAt = DateTime.UtcNow;
                    await invoiceRepo.UpdateAsync(invoice);

                    var payment = new Payment
                    {
                        InvoiceId = invoice.Id,
                        UserId = invoice.UserId,
                        Gateway = "webhook",
                        TransactionId = result.TransactionId,
                        Amount = result.Amount,
                        Status = PaymentStatus.Completed
                    };
                    await paymentRepo.CreateAsync(payment);

                    var order = await orderRepo.GetByIdAsync(invoice.OrderId);
                    if (order != null && order.Status == OrderStatus.Pending)
                    {
                        order.Status = OrderStatus.Active;
                        await orderRepo.UpdateAsync(order);
                    }
                }
                break;

            case WebhookEventType.PaymentFailed:
                logger.LogWarning("Payment failed for invoice {InvoiceId}", result.InvoiceId);
                break;

            case WebhookEventType.PaymentRefunded:
                var refundInvoice = await invoiceRepo.GetByIdAsync(result.InvoiceId!);
                if (refundInvoice != null)
                {
                    refundInvoice.Status = InvoiceStatus.Refunded;
                    await invoiceRepo.UpdateAsync(refundInvoice);
                }
                break;
        }
    }
}
