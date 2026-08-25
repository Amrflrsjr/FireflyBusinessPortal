using Firefly.Application.Customers.Dtos;
using Firefly.Application.Quotations.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IQuotationService
    {
        Task<IEnumerable<QuotationResponseDto>> GetAllQuotationsAsync(string? search = null,
         string? status = null,
         DateTime? startDate = null,
         DateTime? endDate = null,
         string? sortBy = null,
         bool ascending = true);
        Task<QuotationResponseDto?> GetQuotationByIdAsync(int id);
        Task<QuotationResponseDto> CreateQuotationAsync(CreateQuotationDto dto, string userId);
        Task<bool> UpdateQuotationAsync(int id, UpdateQuotationDto dto, string userId);
        Task<bool> UpdateStatusAsync(int id, UpdateQuotationStatusDto dto);
        Task<bool> DeleteQuotationAsync(int id);
        Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id);
        Task<IEnumerable<QuotationResponseDto>> GetDeletedQuotationsAsync();
        Task<bool> RestoreQuotationAsync(int id);
        Task<bool> PermanentlyDeleteQuotationAsync(int id);
    }
}