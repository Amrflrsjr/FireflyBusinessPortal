using Firefly.Application.Customers.Dtos;
using Firefly.Application.Quotations.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IQuotationService
    {
        Task<IEnumerable<QuotationResponseDto>> GetAllQuotationsAsync();
        Task<QuotationResponseDto?> GetQuotationByIdAsync(int id);
        Task<QuotationResponseDto> CreateQuotationAsync(CreateQuotationDto dto, string userId);
        Task<bool> UpdateStatusAsync(int id, UpdateQuotationStatusDto dto);
        Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id);
        Task<bool> DeleteQuotationAsync(int id);
        Task<IEnumerable<QuotationResponseDto>> GetDeletedQuotationsAsync();
        Task<bool> RestoreQuotationAsync(int id);
        Task<bool> PermanentlyDeleteQuotationAsync(int id);
    }
}