namespace OpenGate.Application.Interfaces;

public interface IInvoicePdfService
{
    Task<byte[]> GeneratePdfAsync(string invoiceId);
}
