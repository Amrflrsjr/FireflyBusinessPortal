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

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> DownloadPdf(
            int id,
            [FromServices] IPdfService pdfService)
        {
            var quotation = await _quotationService.GetQuotationByIdAsync(id);
            if (quotation == null) return NotFound();

            var pdfBytes = pdfService.GenerateQuotationPdf(quotation);
            return File(pdfBytes, "application/pdf", $"Estimate_{quotation.QuotationNumber}.pdf");
        }

        [HttpGet("{id:int}/email-preview")]
        public async Task<IActionResult> GetEmailPreview(int id)
        {
            var preview = await _quotationService.GetEmailPreviewAsync(id);
            if (preview == null) return NotFound();

            return Ok(preview);
        }

        [HttpPost("{id:int}/send-email")]
        public async Task<IActionResult> SendEmail(
            int id,
            [FromBody] SendEmailRequestDto dto,
            [FromServices] IPdfService pdfService,
            [FromServices] IEmailService emailService)
        {
            var quotation = await _quotationService.GetQuotationByIdAsync(id);
            if (quotation == null) return NotFound();

            var pdfBytes = pdfService.GenerateQuotationPdf(quotation);

            await emailService.SendDocumentEmailAsync(
                dto.RecipientEmails,
                dto.Subject,
                dto.Body,
                pdfBytes,
                $"Estimate_{quotation.QuotationNumber}.pdf"
            );

            await _quotationService.UpdateStatusAsync(id, new UpdateQuotationStatusDto("Sent"));

            return Ok(new { message = "Email sent successfully." });
        }
    }
}