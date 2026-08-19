using Firefly.Application.Search.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface ISearchService
    {
        Task<GlobalSearchResponseDto> SearchAllAsync(string query);
    }
}