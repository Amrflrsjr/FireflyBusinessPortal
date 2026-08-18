using Firefly.Application.Common.Interfaces;
using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Firefly.Infrastructure.Services
{
    public class PdfService : IPdfService
    {
        public byte[] GenerateQuotationPdf(QuotationResponseDto q)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    // Header
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("NXF Sticker Shop").Bold().FontSize(16).FontColor(Colors.Green.Darken2);
                            col.Item().Text("Unit #26, 2nd Flr, J&G Bldg, H. Abellana St., Canduman");
                            col.Item().Text("Mandaue City, Cebu 6014");
                            col.Item().Text("fireflycraftscebu@gmail.com");
                            col.Item().Text("www.fireflycraftsph.com");
                        });

                        row.ConstantItem(150).Column(col =>
                        {
                            col.Item().AlignRight().Text("Estimate").Bold().FontSize(20).FontColor(Colors.Grey.Darken2);
                            col.Item().AlignRight().Text($"ESTIMATE {q.QuotationNumber}").Bold();
                            col.Item().AlignRight().Text($"DATE {q.DateGenerated:MM/dd/yyyy}");
                        });
                    });

                    // Address and Ship To
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("ADDRESS").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(q.CompanyName).Bold();
                                c.Item().Text(q.ContactNameSnapshot);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("SHIP TO").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(q.CompanyName).Bold();
                                c.Item().Text(q.ContactNameSnapshot);
                            });
                        });

                        col.Item().PaddingTop(15);

                        // Items Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(90);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("ACTIVITY DESCRIPTION").Bold();
                                header.Cell().AlignRight().Text("QTY").Bold();
                                header.Cell().AlignRight().Text("RATE").Bold();
                                header.Cell().AlignRight().Text("TAX").Bold();
                                header.Cell().AlignRight().Text("AMOUNT").Bold();
                            });

                            foreach (var item in q.Items)
                            {
                                table.Cell().Text(item.Description);
                                table.Cell().AlignRight().Text(item.Quantity.ToString());
                                table.Cell().AlignRight().Text($"{item.UnitPrice:N2}");
                                table.Cell().AlignRight().Text(q.VATType == "Inclusive" || q.VATType == "Exclusive" ? "12% S" : "0%");
                                table.Cell().AlignRight().Text($"{item.TotalAmount:N2}");
                            }
                        });

                        col.Item().PaddingTop(15);

                        // Payment Details & Totals
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("To proceed with this transaction, we require 50% downpayment.").FontSize(9);
                                c.Item().Text("Bank: Metrobank").Bold().FontSize(9);
                                c.Item().Text("Account Name: NXF STICKER SHOP").FontSize(9);
                                c.Item().Text("Account Number: 351-3-35157604-0").FontSize(9);
                                c.Item().PaddingTop(5);
                                c.Item().Text("GCash").Bold().FontSize(9);
                                c.Item().Text("Account Name: X** A*").FontSize(9);
                                c.Item().Text("Account Number: 0917-138-6938").FontSize(9);
                            });

                            row.ConstantItem(200).Column(c =>
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("SUBTOTAL").Bold();
                                    r.ConstantItem(80).AlignRight().Text($"{q.Subtotal:N2}");
                                });
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TAX").Bold();
                                    r.ConstantItem(80).AlignRight().Text($"{q.VATAmount:N2}");
                                });
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TOTAL").Bold().FontSize(12);
                                    r.ConstantItem(80).AlignRight().Text($"PHP {q.TotalAmount:N2}").Bold().FontSize(12);
                                });
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateInvoicePdf(InvoiceResponseDto invoice)
        {
            // Reuses QuestPDF layout for Invoice document type
            return GenerateQuotationPdf(new QuotationResponseDto(
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                invoice.CustomerId,
                invoice.CompanyName,
                0,
                invoice.ContactNameSnapshot,
                invoice.ContactEmailSnapshot,
                "",
                invoice.IssueDate,
                invoice.DueDate,
                invoice.VATType,
                invoice.Status,
                invoice.Notes,
                invoice.Subtotal,
                invoice.VATAmount,
                invoice.TotalAmount,
                invoice.CreatedAt,
                new List<QuotationItemResponseDto>()
            ));
        }
    }
}