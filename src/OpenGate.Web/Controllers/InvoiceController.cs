using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGate.Application.Interfaces;

namespace OpenGate.Web.Controllers;

[Route("api/invoices")]
[ApiController]
[Authorize]
public class InvoiceController(IInvoicePdfService pdfService, IInvoiceService invoiceService) : ControllerBase
{
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        try
        {
            var invoice = await invoiceService.GetByIdAsync(id);
            if (invoice == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && invoice.UserId != userId)
                return Forbid();

            var pdf = await pdfService.GeneratePdfAsync(id);
            return File(pdf, "application/pdf", $"invoice-{id}.pdf");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
