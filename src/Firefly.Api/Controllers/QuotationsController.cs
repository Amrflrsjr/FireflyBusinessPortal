using System.Security.Claims;
using Firefly.Application.Common.Interfaces;
using Firefly.Application.Quotations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class QuotationsController : ControllerBase
    {
        private readonly IQuotationService _quotationService;

        public QuotationsController(IQuotationService quotationService)
        {
            _quotationService = quotationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var quotations = await _quotationService.GetAllQuotationsAsync();
            return Ok(quotations);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var quotation = await _quotationService.GetQuotationByIdAsync(id);
            if (quotation == null) return NotFound();
            return Ok(quotation);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateQuotationDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var quotation = await _quotationService.CreateQuotationAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = quotation.QuotationId }, quotation);
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateQuotationStatusDto dto)
        {
            var updated = await _quotationService.UpdateStatusAsync(id, dto);
            if (!updated) return NotFound();
            return NoContent();
        }
    }
}