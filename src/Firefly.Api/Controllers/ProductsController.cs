using Firefly.Application.Common.Interfaces;
using Firefly.Application.Products.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? sortBy,
            [FromQuery] bool ascending = true)
        {
            var products = await _productService.GetAllProductsAsync(search, sortBy, ascending);
            return Ok(products);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
        {
            var product = await _productService.CreateProductAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = product.ProductId }, product);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
        {
            var updated = await _productService.UpdateProductAsync(id, dto);
            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _productService.DeleteProductAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpPost("{id:int}/variants")]
        public async Task<IActionResult> AddVariant(int id, [FromBody] CreateProductVariantDto dto)
        {
            var variant = await _productService.AddVariantAsync(id, dto);
            if (variant == null) return NotFound();
            return Ok(variant);
        }

        [HttpPut("variants/{variantId:int}")]
        public async Task<IActionResult> UpdateVariant(int variantId, [FromBody] UpdateProductVariantDto dto)
        {
            var updated = await _productService.UpdateVariantAsync(variantId, dto);
            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpDelete("variants/{variantId:int}")]
        public async Task<IActionResult> DeleteVariant(int variantId)
        {
            var result = await _productService.DeleteVariantAsync(variantId);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeletedProducts([FromQuery] string? search)
        {
            var deletedProducts = await _productService.GetDeletedProductsAsync(search);
            return Ok(deletedProducts);
        }

        [HttpPost("{id:int}/restore")]
        public async Task<IActionResult> RestoreProduct(int id)
        {
            bool result = await _productService.RestoreProductAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteProduct(int id)
        {
            bool result = await _productService.PermanentlyDeleteProductAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpGet("variants/deleted")]
        public async Task<IActionResult> GetDeletedVariants([FromQuery] string? search)
        {
            var deletedVariants = await _productService.GetDeletedVariantsAsync(search);
            return Ok(deletedVariants);
        }

        [HttpPost("variants/{variantId:int}/restore")]
        public async Task<IActionResult> RestoreVariant(int variantId)
        {
            bool result = await _productService.RestoreVariantAsync(variantId);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpDelete("variants/{variantId:int}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteVariant(int variantId)
        {
            bool result = await _productService.PermanentlyDeleteVariantAsync(variantId);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}