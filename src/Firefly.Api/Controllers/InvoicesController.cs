using Firefly.Application.Common.Interfaces;
using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
         [FromQuery] string? search,
         [FromQuery] string? status,
         [FromQuery] DateTime? startDate,
         [FromQuery] DateTime? endDate,
         [FromQuery] string? sortBy,
         [FromQuery] bool ascending = true)
        {
            var invoices = await _invoiceService.GetAllInvoicesAsync(search, status, startDate, endDate, sortBy, ascending);
            return Ok(invoices);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null) return NotFound();
            return Ok(invoice);
        }

        [HttpPost("from-quotation")]
        public async Task<IActionResult> CreateFromQuotation([FromBody] CreateInvoiceFromQuotationDto dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                var invoice = await _invoiceService.CreateInvoiceFromQuotationAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = invoice.InvoiceId }, invoice);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _invoiceService.DeleteInvoiceAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpPost("{id:int}/payments")]
        public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordPaymentDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var payment = await _invoiceService.RecordPaymentAsync(id, dto, userId);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> DownloadPdf(int id, [FromServices] IPdfService pdfService)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null) return NotFound();

            var pdfBytes = pdfService.GenerateInvoicePdf(invoice);
            return File(pdfBytes, "application/pdf", $"Invoice_{invoice.InvoiceNumber}.pdf");
        }

        [HttpGet("{id:int}/email-preview")]
        public async Task<IActionResult> GetEmailPreview(int id)
        {
            var preview = await _invoiceService.GetEmailPreviewAsync(id);
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
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null) return NotFound();

            var pdfBytes = pdfService.GenerateInvoicePdf(invoice);

            await emailService.SendDocumentEmailAsync(
                dto.RecipientEmails,
                dto.Subject,
                dto.Body,
                pdfBytes,
                $"Invoice_{invoice.InvoiceNumber}.pdf"
            );

            return Ok(new { message = "Invoice email sent successfully." });
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateInvoiceStatusDto dto)
        {
            var updated = await _invoiceService.UpdateStatusAsync(id, dto.Status);
            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeletedInvoices([FromQuery] string? search)
        {
            var deletedInvoices = await _invoiceService.GetDeletedInvoicesAsync(search);
            return Ok(deletedInvoices);
        }

        [HttpPost("{id:int}/restore")]
        public async Task<IActionResult> RestoreInvoice(int id)
        {
            bool result = await _invoiceService.RestoreInvoiceAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteInvoice(int id)
        {
            bool result = await _invoiceService.PermanentlyDeleteInvoiceAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}