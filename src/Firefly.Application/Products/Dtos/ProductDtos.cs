namespace Firefly.Application.Products.Dtos
{
    public record CreateProductDto(
        string Name,
        string Description,
        List<CreateProductVariantDto>? Variants
    );

    public record UpdateProductDto(
        string Name,
        string Description,
        bool IsActive
    );

    public record CreateProductVariantDto(
        string SKU,
        string Color,
        string Size,
        decimal UnitPrice,
        int Stock
    );

    public record UpdateProductVariantDto(
        string SKU,
        string Color,
        string Size,
        decimal UnitPrice,
        int Stock,
        bool IsActive
    );

    public record ProductResponseDto(
        int ProductId,
        string Name,
        string Description,
        bool IsActive,
        DateTime CreatedAt,
        List<ProductVariantResponseDto> Variants
    );

    public record ProductVariantResponseDto(
        int ProductVariantId,
        int ProductId,
        string SKU,
        string Color,
        string Size,
        decimal UnitPrice,
        int Stock,
        bool IsActive
    );
}