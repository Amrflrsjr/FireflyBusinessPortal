using Firefly.Application.Common.Interfaces;
using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;

namespace Firefly.Infrastructure.Services
{
    public class PdfService : IPdfService
    {
        public byte[] GenerateQuotationPdf(QuotationResponseDto q)
        {
            return GenerateDocumentPdf(q, "ESTIMATE");
        }

        public byte[] GenerateInvoicePdf(InvoiceResponseDto invoice)
        {
            var mappedDocument = new QuotationResponseDto(
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
            );

            return GenerateDocumentPdf(mappedDocument, "INVOICE");
        }

        private byte[] GenerateDocumentPdf(QuotationResponseDto q, string documentTitle)
        {
            byte[] logoBytes = null;
            try
            {
                // Try multiple potential file paths where the logo might reside at runtime
                string[] possiblePaths = {
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Firefly Logo - No BG.png"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Firefly.Api", "Assets", "Firefly Logo - No BG.png"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Firefly Logo - No BG.png")
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        logoBytes = File.ReadAllBytes(path);
                        break;
                    }
                }
            }
            catch
            {
                logoBytes = null;
            }

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    // Header with Logo
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                if (logoBytes != null && logoBytes.Length > 0)
                                {
                                    col.Item().Width(95).Image(logoBytes);
                                    col.Item().PaddingTop(4);
                                }
                                col.Item().Text("NXF Sticker Shop").Bold().FontSize(11).FontColor(Colors.Grey.Darken4);
                                col.Item().Text("Unit #26, 2nd Flr, J&G Bldg, H. Abellana St., Canduman").FontSize(9);
                                col.Item().Text("Mandaue City, Cebu 6014").FontSize(9);
                                col.Item().Text("fireflycraftscebu@gmail.com").FontSize(9);
                                col.Item().Text("www.fireflycraftsph.com").FontSize(9);
                            });

                            row.ConstantItem(180).Column(col =>
                            {
                                col.Item().AlignRight().Text(documentTitle).Bold().FontSize(22).FontColor(Colors.Grey.Darken2);
                                col.Item().AlignRight().Text($"{q.QuotationNumber}").Bold().FontSize(10);
                                col.Item().AlignRight().Text($"DATE {q.DateGenerated:MM/dd/yyyy}").FontSize(10);
                            });
                        });

                        // Subtle Divider Line below header
                        headerCol.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    // Address and Ship To Section
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

                        col.Item().PaddingTop(20);

                        // Professional Styled Items Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(40);
                                columns.ConstantColumn(75);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(85);
                            });

                            // Table Header with Dark Background
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("ACTIVITY DESCRIPTION").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("QTY").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("RATE").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("TAX").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("AMOUNT").Bold().FontColor(Colors.White).FontSize(9);
                            });

                            // Table Rows
                            bool alternate = false;
                            foreach (var item in q.Items)
                            {
                                var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(bgColor).Padding(6).Text(item.Description).FontSize(9);
                                table.Cell().Background(bgColor).Padding(6).AlignRight().Text(item.Quantity.ToString()).FontSize(9);
                                table.Cell().Background(bgColor).Padding(6).AlignRight().Text($"{item.UnitPrice:N2}").FontSize(9);
                                table.Cell().Background(bgColor).Padding(6).AlignRight().Text(q.VATType.Contains("Inclusive") || q.VATType.Contains("Exclusive") ? "12% S" : "0%").FontSize(9);
                                table.Cell().Background(bgColor).Padding(6).AlignRight().Text($"{item.TotalAmount:N2}").FontSize(9);

                                alternate = !alternate;
                            }
                        });

                        col.Item().PaddingTop(20);

                        // Payment Details & Totals Breakdown Section
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("To proceed with this transaction, we require 50% downpayment.").FontSize(9);
                                c.Item().PaddingTop(6);
                                c.Item().Text("Bank: Metrobank").Bold().FontSize(9);
                                c.Item().Text("Account Name: NXF STICKER SHOP").FontSize(9);
                                c.Item().Text("Account Number: 351-3-35157604-0").FontSize(9);
                                c.Item().PaddingTop(6);
                                c.Item().Text("GCash").Bold().FontSize(9);
                                c.Item().Text("Account Name: X** A*").FontSize(9);
                                c.Item().Text("Account Number: 0917-138-6938").FontSize(9);

                                c.Item().PaddingTop(25);
                                c.Item().Row(sig =>
                                {
                                    sig.RelativeItem().Column(s => {
                                        s.Item().Text("Accepted By: ___________________________").FontSize(9);
                                    });
                                    sig.RelativeItem().Column(s => {
                                        s.Item().Text($"Accepted Date: {DateTime.Now:MM/dd/yyyy}").FontSize(9);
                                    });
                                });
                            });

                            row.ConstantItem(210).Column(c =>
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("SUBTOTAL").Bold().FontSize(9);
                                    r.ConstantItem(90).AlignRight().Text($"{q.Subtotal:N2}").FontSize(9);
                                });
                                c.Item().PaddingTop(4);
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TAX").Bold().FontSize(9);
                                    r.ConstantItem(90).AlignRight().Text($"{q.VATAmount:N2}").FontSize(9);
                                });
                                c.Item().PaddingTop(6);
                                c.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                                c.Item().PaddingTop(6);
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                                    r.ConstantItem(90).AlignRight().Text($"PHP {q.TotalAmount:N2}").Bold().FontSize(11);
                                });
                            });
                        });
                    });

                    // Footer Page Numbering
                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();
        }
    }
}