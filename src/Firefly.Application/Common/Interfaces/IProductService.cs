using Firefly.Application.Products.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync();
        Task<ProductResponseDto?> GetProductByIdAsync(int id);
        Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto);
        Task<bool> UpdateProductAsync(int id, UpdateProductDto dto);
        Task<ProductVariantResponseDto?> AddVariantAsync(int productId, CreateProductVariantDto dto);
        Task<bool> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto);
    }
}