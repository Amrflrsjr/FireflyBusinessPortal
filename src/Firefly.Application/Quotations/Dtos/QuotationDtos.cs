namespace Firefly.Application.Quotations.Dtos
{
    public record CreateQuotationItemDto(
        int? ProductVariantId,
        string Description,
        int Quantity,
        decimal UnitPrice
    );

    public record CreateQuotationDto(
        int CustomerId,
        int? ContactId,
        DateTime ValidUntil,
        string VATType,
        string? NoteToCustomer,
        string? Status,
        List<CreateQuotationItemDto> Items
    );

    public record UpdateQuotationDto(
        int CustomerId,
        int? ContactId,
        DateTime ValidUntil,
        string VATType,
        string? NoteToCustomer,
        List<CreateQuotationItemDto> Items
    );

    // Ensure this record is present and public
    public record UpdateQuotationStatusDto(
        string Status
    );

    public record QuotationItemResponseDto(
        int QuotationItemId,
        int? ProductVariantId,
        string Description,
        int Quantity,
        decimal UnitPrice,
        decimal TotalAmount,
        string? ProductName,
        string? SKU,
        string? Color,
        string? Size
    );

    public record QuotationResponseDto(
        int QuotationId,
        string QuotationNumber,
        int CustomerId,
        string CompanyName,
        string CompanyAddress,
        string TIN,
        int? ContactId,
        string ContactNameSnapshot,
        string ContactEmailSnapshot,
        string ContactPositionSnapshot,
        DateTime DateGenerated,
        DateTime ValidUntil,
        string VATType,
        string Status,
        string? NoteToCustomer,
        decimal Subtotal,
        decimal VATAmount,
        decimal TotalAmount,
        DateTime CreatedAt,
        List<QuotationItemResponseDto> Items
    );
}