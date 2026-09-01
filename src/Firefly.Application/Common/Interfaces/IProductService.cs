using Firefly.Application.Products.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync(string? search = null, string? sortBy = null, bool ascending = true);
        Task<ProductResponseDto?> GetProductByIdAsync(int id);
        Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto);
        Task<bool> UpdateProductAsync(int id, UpdateProductDto dto);
        Task<ProductVariantResponseDto?> AddVariantAsync(int productId, CreateProductVariantDto dto);
        Task<bool> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto);
        Task<bool> DeleteProductAsync(int id);
        Task<bool> DeleteVariantAsync(int variantId);
        Task<IEnumerable<ProductResponseDto>> GetDeletedProductsAsync(string? search);
        Task<bool> RestoreProductAsync(int id);
        Task<bool> PermanentlyDeleteProductAsync(int id);

        Task<IEnumerable<ProductVariantResponseDto>> GetDeletedVariantsAsync(string? search = null);
        Task<bool> RestoreVariantAsync(int variantId);
        Task<bool> PermanentlyDeleteVariantAsync(int variantId);
    }
}