using Firefly.Application.Customers.Dtos;
using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IInvoiceService
    {
        Task<IEnumerable<InvoiceResponseDto>> GetAllInvoicesAsync(
             string? search = null,
             string? status = null,
             DateTime? startDate = null,
             DateTime? endDate = null,
             string? sortBy = null,
             bool ascending = true
         );
        Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int id);
        Task<InvoiceResponseDto> CreateInvoiceFromQuotationAsync(CreateInvoiceFromQuotationDto dto, string userId);
        Task<PaymentResponseDto?> RecordPaymentAsync(int invoiceId, RecordPaymentDto dto, string userId);
        Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id);
        Task<bool> DeleteInvoiceAsync(int id);
        Task<IEnumerable<InvoiceResponseDto>> GetDeletedInvoicesAsync(string? search);
        Task<bool> UpdateStatusAsync(int id, string status);
        Task<bool> RestoreInvoiceAsync(int id);
        Task<bool> PermanentlyDeleteInvoiceAsync(int id);
    }
}