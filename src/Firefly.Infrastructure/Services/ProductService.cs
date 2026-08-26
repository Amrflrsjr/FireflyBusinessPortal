using Firefly.Application.Common.Interfaces;
using Firefly.Application.Products.Dtos;
using Firefly.Domain.Entities;
using Firefly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync(string? search = null, string? sortBy = null, bool ascending = true)
        {
            var query = _context.Products
                .Include(p => p.Variants)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(p =>
                    p.ProductId.ToString() == search ||
                    p.Name.ToLower().Contains(search) ||
                    p.Description.ToLower().Contains(search) ||
                    p.Variants.Any(v => v.SKU.ToLower().Contains(search))
                );
            }

            // Apply dynamic sorting
            query = sortBy?.ToLower() switch
            {
                "description" => ascending ? query.OrderBy(p => p.Description) : query.OrderByDescending(p => p.Description),
                "createdat" => ascending ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt),
                "name" or _ => ascending ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            };

            return await query
                .Select(p => new ProductResponseDto(
                    p.ProductId,
                    p.Name,
                    p.Description,
                    p.IsActive,
                    p.CreatedAt,
                    p.Variants.Where(v => v.IsActive).Select(v => new ProductVariantResponseDto(
                        v.ProductVariantId,
                        v.ProductId,
                        v.SKU,
                        v.Color,
                        v.Size,
                        v.UnitPrice,
                        v.Stock,
                        v.IsActive
                    )).ToList()
                ))
                .ToListAsync();
        }

        public async Task<ProductResponseDto?> GetProductByIdAsync(int id)
        {
            var p = await _context.Products
                .Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.ProductId == id && x.IsActive);

            if (p == null) return null;

            return new ProductResponseDto(
                p.ProductId,
                p.Name,
                p.Description,
                p.IsActive,
                p.CreatedAt,
                p.Variants.Where(v => v.IsActive).Select(v => new ProductVariantResponseDto(
                    v.ProductVariantId,
                    v.ProductId,
                    v.SKU,
                    v.Color,
                    v.Size,
                    v.UnitPrice,
                    v.Stock,
                    v.IsActive
                )).ToList()
            );
        }

        public async Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto)
        {
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.Variants != null)
            {
                foreach (var v in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        SKU = v.SKU,
                        Color = v.Color,
                        Size = v.Size,
                        UnitPrice = v.UnitPrice,
                        Stock = v.Stock,
                        IsActive = true
                    });
                }
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return (await GetProductByIdAsync(product.ProductId))!;
        }

        public async Task<bool> UpdateProductAsync(int id, UpdateProductDto dto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return false;

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.IsActive = dto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.ProductId == id && p.IsActive);

            if (product == null) return false;

            product.IsActive = false;
            foreach (var variant in product.Variants)
            {
                variant.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ProductVariantResponseDto?> AddVariantAsync(int productId, CreateProductVariantDto dto)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return null;

            var variant = new ProductVariant
            {
                ProductId = productId,
                SKU = dto.SKU,
                Color = dto.Color,
                Size = dto.Size,
                UnitPrice = dto.UnitPrice,
                Stock = dto.Stock,
                IsActive = true
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            return new ProductVariantResponseDto(
                variant.ProductVariantId,
                variant.ProductId,
                variant.SKU,
                variant.Color,
                variant.Size,
                variant.UnitPrice,
                variant.Stock,
                variant.IsActive
            );
        }

        public async Task<bool> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto)
        {
            var variant = await _context.ProductVariants.FindAsync(variantId);
            if (variant == null) return false;

            variant.SKU = dto.SKU;
            variant.Color = dto.Color;
            variant.Size = dto.Size;
            variant.UnitPrice = dto.UnitPrice;
            variant.Stock = dto.Stock;
            variant.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteVariantAsync(int variantId)
        {
            var variant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.ProductVariantId == variantId && v.IsActive);
            if (variant == null) return false;

            variant.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ProductResponseDto>> GetDeletedProductsAsync(string? search = null)
        {
            var query = _context.Products
                .Include(p => p.Variants)
                .Where(p => !p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(search) ||
                    (!string.IsNullOrEmpty(p.Description) && p.Description.ToLower().Contains(search)) ||
                    p.Variants.Any(v => v.SKU.ToLower().Contains(search))
                );
            }

            return await query
                .Select(p => new ProductResponseDto(
                    p.ProductId,
                    p.Name,
                    p.Description,
                    p.IsActive,
                    p.CreatedAt,
                    p.Variants.Select(v => new ProductVariantResponseDto(
                        v.ProductVariantId,
                        v.ProductId,
                        v.SKU,
                        v.Color,
                        v.Size,
                        v.UnitPrice,
                        v.Stock,
                        v.IsActive
                    )).ToList()
                ))
                .ToListAsync();
        }

        public async Task<bool> RestoreProductAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return false;

            product.IsActive = true;
            foreach (var variant in product.Variants)
            {
                variant.IsActive = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PermanentlyDeleteProductAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return false;

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}