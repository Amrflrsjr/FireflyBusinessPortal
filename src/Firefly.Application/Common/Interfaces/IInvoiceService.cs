using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IInvoiceService
    {
        Task<IEnumerable<InvoiceResponseDto>> GetAllInvoicesAsync();
        Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int id);
        Task<InvoiceResponseDto> CreateInvoiceFromQuotationAsync(CreateInvoiceFromQuotationDto dto, string userId);
        Task<PaymentResponseDto?> RecordPaymentAsync(int invoiceId, RecordPaymentDto dto, string userId);
        Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id);
        Task<bool> DeleteInvoiceAsync(int id);
    }
}