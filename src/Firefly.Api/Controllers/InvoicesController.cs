using System.Security.Claims;
using Firefly.Application.Common.Interfaces;
using Firefly.Application.Invoices.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetAll()
        {
            var invoices = await _invoiceService.GetAllInvoicesAsync();
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var invoice = await _invoiceService.CreateInvoiceFromQuotationAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = invoice.InvoiceId }, invoice);
        }

        [HttpPost("{id:int}/payments")]
        public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordPaymentDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var payment = await _invoiceService.RecordPaymentAsync(id, dto, userId);
            if (payment == null) return NotFound();
            return Ok(payment);
        }
    }
}