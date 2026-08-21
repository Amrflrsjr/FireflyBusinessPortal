using Firefly.Application.Common.Interfaces;
using Firefly.Application.Invoices.Dtos;
using Firefly.Application.Quotations.Dtos;
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
                .Where(i => i.Status != "Cancelled") // Filter out soft-deleted/cancelled invoices[cite: 22]
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
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.Status != "Cancelled"); // Ensure active invoice[cite: 22]

            if (invoice == null) return null;
            return MapToDto(invoice);
        }

        public async Task<InvoiceResponseDto> CreateInvoiceFromQuotationAsync(CreateInvoiceFromQuotationDto dto, string userId)
        {
            // 1. Prevent duplicate conversions (Idempotency Check) excluding cancelled ones[cite: 22]
            var existingInvoice = await _context.Invoices
                .AnyAsync(i => i.QuotationId == dto.QuotationId && i.Status != "Cancelled");

            if (existingInvoice)
                throw new InvalidOperationException("An invoice has already been generated for this quotation.");

            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.QuotationId == dto.QuotationId && q.Status != "Cancelled");

            if (quotation == null)
                throw new KeyNotFoundException("Quotation not found.");

            // 2. Wrap the database operations in a transaction[cite: 22]
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
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

                // Update the quotation status[cite: 22]
                quotation.Status = "Accepted";

                _context.Invoices.Add(invoice);

                // Save changes and commit transaction[cite: 22]
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (await GetInvoiceByIdAsync(invoice.InvoiceId))!;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Soft delete implementation for Invoice via status update to Cancelled[cite: 22]
        public async Task<bool> DeleteInvoiceAsync(int id)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && i.Status != "Cancelled");
            if (invoice == null) return false;

            invoice.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PaymentResponseDto?> RecordPaymentAsync(int invoiceId, RecordPaymentDto dto, string userId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Status != "Cancelled"); // Ensure active invoice[cite: 22]

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

            // Recalculate totals[cite: 22]
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

        public async Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id)
        {
            var invoice = await _context.Invoices
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.InvoiceId == id && x.Status != "Cancelled"); // Ensure active invoice[cite: 22]

            if (invoice == null) return null;

            var settings = await _context.CompanySettings.FirstOrDefaultAsync();

            string subject = $"Invoice #{invoice.InvoiceNumber} - {invoice.Customer?.CompanyName}";
            string pdfFileName = $"Invoice_{invoice.InvoiceNumber}.pdf";
            string body = $"Dear {invoice.ContactNameSnapshot},\n\nPlease find attached the invoice {invoice.InvoiceNumber} for your review and payment.\n\nThank you,\n{settings?.CompanyName ?? "NXF Sticker Shop"}";

            var recipients = new List<string>();
            if (!string.IsNullOrEmpty(invoice.ContactEmailSnapshot))
            {
                recipients.Add(invoice.ContactEmailSnapshot);
            }

            return new DocumentEmailPreviewDto(
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                recipients,
                subject,
                body,
                invoice.Customer?.CompanyName ?? string.Empty,
                invoice.ContactNameSnapshot,
                invoice.TotalAmount,
                pdfFileName
            );
        }
        public async Task<IEnumerable<InvoiceResponseDto>> GetDeletedInvoicesAsync()
        {
            return await _context.Invoices
                .Include(i => i.Quotation)
                .ThenInclude(q => q.Customer)
                .Include(i => i.Payments)
                .Where(i => i.Status == "Cancelled")
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => MapToDto(i))
                .ToListAsync();
        }

        public async Task<bool> RestoreInvoiceAsync(int id)
        {
            var invoice = await _context.Invoices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.Status == "Cancelled");

            if (invoice == null) return false;

            // Restore status back to Unpaid (or recalculate balance if needed)
            invoice.Status = invoice.TotalPaid > 0 ? "PartiallyPaid" : "Unpaid";
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PermanentlyDeleteInvoiceAsync(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .Include(i => i.Items)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return false;

            // Permanent hard delete from database (including payments/items cascade)
            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}