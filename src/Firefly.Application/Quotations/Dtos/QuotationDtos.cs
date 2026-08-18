namespace Firefly.Application.Quotations.Dtos
{
    public record CreateQuotationItemDto(
        int ProductVariantId,
        string Description,
        int Quantity,
        decimal UnitPrice
    );

    public record CreateQuotationDto(
        int CustomerId,
        int ContactId,
        DateTime ValidUntil,
        string VATType, // "Inclusive", "Exclusive", "ZeroRated", "OutOfScope"
        string NoteToCustomer,
        List<CreateQuotationItemDto> Items
    );

    public record UpdateQuotationStatusDto(
        string Status // "Created", "Sent", "Accepted", "Rejected", "Expired"
    );

    public record QuotationItemResponseDto(
        int QuotationItemId,
        int ProductVariantId,
        string Description,
        int Quantity,
        decimal UnitPrice,
        decimal TotalAmount
    );

    public record QuotationResponseDto(
        int QuotationId,
        string QuotationNumber,
        int CustomerId,
        string CompanyName,
        int ContactId,
        string ContactNameSnapshot,
        string ContactEmailSnapshot,
        string ContactPositionSnapshot,
        DateTime DateGenerated,
        DateTime ValidUntil,
        string VATType,
        string Status,
        string NoteToCustomer,
        decimal Subtotal,
        decimal VATAmount,
        decimal TotalAmount,
        DateTime CreatedAt,
        List<QuotationItemResponseDto> Items
    );
}