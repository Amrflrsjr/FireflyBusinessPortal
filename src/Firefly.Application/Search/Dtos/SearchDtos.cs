namespace Firefly.Application.Search.Dtos
{
    public record GlobalSearchResponseDto(
        List<SearchItemDto> Customers,
        List<SearchItemDto> Quotations,
        List<SearchItemDto> Invoices,
        List<SearchItemDto> Products
    );

    public record SearchItemDto(
        int Id,
        string Title,
        string Subtitle,
        string Type,
        string Url
    );
}