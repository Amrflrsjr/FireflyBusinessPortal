using Firefly.Application.Common.Interfaces;
using Firefly.Application.Invoices.Dtos;
using Firefly.Domain.Entities;
using Firefly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly ApplicationDbContext _context;

        public InvoiceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<InvoiceResponseDto>> GetAllInvoicesAsync()
        {
            return await _context.Invoices
                .Include(i => i.Quotation)
                .ThenInclude(q => q.Customer)
                .Include(i => i.Payments)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => MapToDto(i))
                .ToListAsync();
        }

        public async Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Quotation)
                .ThenInclude(q => q.Customer)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return null;
            return MapToDto(invoice);
        }

        public async Task<InvoiceResponseDto> CreateInvoiceFromQuotationAsync(CreateInvoiceFromQuotationDto dto, string userId)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.QuotationId == dto.QuotationId);

            if (quotation == null)
                throw new KeyNotFoundException("Quotation not found.");

            var todayPrefix = $"INV-{DateTime.UtcNow:yyyyMMdd}";
            var countToday = await _context.Invoices
                .CountAsync(i => i.InvoiceNumber.StartsWith(todayPrefix));
            var invoiceNumber = $"{todayPrefix}-{(countToday + 1):D4}";

            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                QuotationId = quotation.QuotationId,
                CustomerId = quotation.CustomerId,
                ContactId = quotation.ContactId,
                ContactNameSnapshot = quotation.ContactNameSnapshot,
                ContactEmailSnapshot = quotation.ContactEmailSnapshot,
                ContactPositionSnapshot = quotation.ContactPositionSnapshot,
                IssueDate = DateTime.UtcNow,
                DueDate = dto.DueDate,
                VATType = quotation.VATType,
                Status = "Unpaid",
                Subtotal = quotation.Subtotal,
                VATAmount = quotation.VATAmount,
                TotalAmount = quotation.TotalAmount,
                TotalPaid = 0,
                BalanceDue = quotation.TotalAmount,
                Notes = dto.Notes,
                CreatedByFK = userId,
                CreatedAt = DateTime.UtcNow
            };

            quotation.Status = "Accepted";

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return (await GetInvoiceByIdAsync(invoice.InvoiceId))!;
        }

        public async Task<PaymentResponseDto?> RecordPaymentAsync(int invoiceId, RecordPaymentDto dto, string userId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null) return null;

            var payment = new Payment
            {
                InvoiceId = invoiceId,
                AmountPaid = dto.AmountPaid,
                PaymentDate = dto.PaymentDate,
                PaymentMethod = dto.PaymentMethod,
                ReferenceNumber = dto.ReferenceNumber,
                Notes = dto.Notes,
                RecordedByFK = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            // Recalculate totals
            invoice.TotalPaid += dto.AmountPaid;
            invoice.BalanceDue = invoice.TotalAmount - invoice.TotalPaid;

            if (invoice.BalanceDue <= 0)
            {
                invoice.BalanceDue = 0;
                invoice.Status = "Paid";
            }
            else
            {
                invoice.Status = "PartiallyPaid";
            }

            await _context.SaveChangesAsync();

            return new PaymentResponseDto(
                payment.PaymentId,
                payment.InvoiceId,
                payment.AmountPaid,
                payment.PaymentDate,
                payment.PaymentMethod,
                payment.ReferenceNumber,
                payment.Notes,
                payment.CreatedAt
            );
        }

        private static InvoiceResponseDto MapToDto(Invoice i)
        {
            return new InvoiceResponseDto(
                i.InvoiceId,
                i.InvoiceNumber,
                i.QuotationId ?? 0,
                i.Quotation != null ? i.Quotation.QuotationNumber : string.Empty,
                i.CustomerId,
                i.Quotation?.Customer?.CompanyName ?? string.Empty,
                i.ContactNameSnapshot,
                i.ContactEmailSnapshot,
                i.IssueDate,
                i.DueDate,
                i.VATType,
                i.Status,
                i.Subtotal,
                i.VATAmount,
                i.TotalAmount,
                i.TotalPaid,
                i.BalanceDue,
                i.Notes,
                i.CreatedAt,
                i.Payments.Select(p => new PaymentResponseDto(
                    p.PaymentId,
                    p.InvoiceId,
                    p.AmountPaid,
                    p.PaymentDate,
                    p.PaymentMethod,
                    p.ReferenceNumber,
                    p.Notes,
                    p.CreatedAt
                )).ToList()
            );
        }
    }
}