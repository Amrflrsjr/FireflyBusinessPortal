using Firefly.Application.Invoices.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IInvoiceService
    {
        Task<IEnumerable<InvoiceResponseDto>> GetAllInvoicesAsync();
        Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int id);
        Task<InvoiceResponseDto> CreateInvoiceFromQuotationAsync(CreateInvoiceFromQuotationDto dto, string userId);
        Task<PaymentResponseDto?> RecordPaymentAsync(int invoiceId, RecordPaymentDto dto, string userId);
    }
}