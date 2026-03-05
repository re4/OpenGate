using OpenGate.Application.Interfaces;
using OpenGate.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OpenGate.Web.Services;

public class InvoicePdfService(IInvoiceRepository invoiceRepo, ISettingRepository settingRepo, CurrencyProvider currencyProvider) : IInvoicePdfService
{
    public async Task<byte[]> GeneratePdfAsync(string invoiceId)
    {
        var invoice = await invoiceRepo.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException("Invoice not found");

        var siteName = (await settingRepo.GetByKeyAsync("SiteName"))?.Value ?? "OpenGate";
        var currencyInfo = await currencyProvider.GetAsync();
        var currencySymbol = currencyInfo.Symbol;
        var currency = !string.IsNullOrWhiteSpace(invoice.Currency) ? invoice.Currency : currencyInfo.CurrencyCode;

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(siteName).FontSize(24).Bold().FontColor(Colors.Indigo.Medium);
                        col.Item().Text("INVOICE").FontSize(14).FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().Text($"Invoice #: {invoice.InvoiceNumber}").Bold();
                        col.Item().Text($"Date: {invoice.CreatedAt:MMM dd, yyyy}");
                        col.Item().Text($"Due: {invoice.DueDate:MMM dd, yyyy}");
                        col.Item().Text($"Status: {invoice.Status}").FontColor(
                            invoice.Status == Domain.Enums.InvoiceStatus.Paid ? Colors.Green.Medium : Colors.Red.Medium);
                    });
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Indigo.Medium).Padding(5)
                                .Text("Description").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Indigo.Medium).Padding(5)
                                .Text("Qty").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Indigo.Medium).Padding(5)
                                .Text("Unit Price").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Indigo.Medium).Padding(5)
                                .Text("Total").FontColor(Colors.White).Bold();
                        });

                        foreach (var line in invoice.Lines)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(line.Description);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(line.Quantity.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text($"{currencySymbol}{line.UnitPrice:F2} {currency}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text($"{currencySymbol}{line.Total:F2} {currency}");
                        }
                    });

                    col.Item().PaddingTop(15).AlignRight().Column(totals =>
                    {
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().AlignRight().Text("Subtotal:").Bold();
                            row.ConstantItem(120).AlignRight().Text($"{currencySymbol}{invoice.Subtotal:F2} {currency}");
                        });

                        if (invoice.Tax > 0)
                        {
                            var taxLabel = !string.IsNullOrWhiteSpace(invoice.TaxLabel) ? invoice.TaxLabel : "Tax";
                            var taxDisplay = invoice.TaxRate > 0
                                ? $"{taxLabel} ({invoice.TaxRate:G}%){(invoice.TaxInclusive ? " incl." : "")}:"
                                : $"{taxLabel}:";
                            totals.Item().Row(row =>
                            {
                                row.RelativeItem().AlignRight().Text(taxDisplay);
                                row.ConstantItem(120).AlignRight().Text($"{currencySymbol}{invoice.Tax:F2} {currency}");
                            });
                        }

                        totals.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().AlignRight().Text("Total:").Bold().FontSize(14);
                            row.ConstantItem(120).AlignRight().Text($"{currencySymbol}{invoice.Total:F2} {currency}")
                                .Bold().FontSize(14).FontColor(Colors.Indigo.Medium);
                        });
                    });

                    if (!string.IsNullOrEmpty(invoice.Notes))
                    {
                        col.Item().PaddingTop(30).Column(notes =>
                        {
                            notes.Item().Text("Notes").Bold();
                            notes.Item().Text(invoice.Notes).FontColor(Colors.Grey.Medium);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generated by {siteName}").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(" | Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }
}
