using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGate.Application.Interfaces;

namespace OpenGate.Web.Controllers;

[Route("api/invoices")]
[ApiController]
[Authorize]
public class InvoiceController(IInvoicePdfService pdfService) : ControllerBase
{
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        try
        {
            var pdf = await pdfService.GeneratePdfAsync(id);
            return File(pdf, "application/pdf", $"invoice-{id}.pdf");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
