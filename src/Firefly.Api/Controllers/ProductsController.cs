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
        public async Task<IActionResult> GetAll()
        {
            var products = await _productService.GetAllProductsAsync();
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
    }
}