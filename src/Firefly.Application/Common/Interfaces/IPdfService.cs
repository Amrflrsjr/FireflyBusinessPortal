using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IPdfService
    {
        byte[] GenerateQuotationPdf(QuotationResponseDto quotation);
        byte[] GenerateInvoicePdf(InvoiceResponseDto invoice);
    }
}