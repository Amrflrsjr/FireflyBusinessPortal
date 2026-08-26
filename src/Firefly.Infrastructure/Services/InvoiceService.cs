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

        public async Task<IEnumerable<InvoiceResponseDto>> GetAllInvoicesAsync(
        string? search = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? sortBy = null,
        bool ascending = true)
        {
            var query = _context.Invoices
                .Include(i => i.Quotation)
                    .ThenInclude(q => q!.Customer)
                .Include(i => i.Payments)
                .Include(i => i.Items)
                    .ThenInclude(item => item.ProductVariant!)
                        .ThenInclude(v => v!.Product)
                .Where(i => !i.IsDeleted) // Decoupled: filters out soft-deleted items instead of status
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(lowerSearch) ||
                    (i.Quotation != null && i.Quotation.QuotationNumber.ToLower().Contains(lowerSearch)) || // Added quotation number search
                    (i.Quotation != null && i.Quotation.Customer != null && i.Quotation.Customer.CompanyName.ToLower().Contains(lowerSearch))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
            {
                query = query.Where(i => i.Status.ToLower() == status.ToLower());
            }

            if (startDate.HasValue)
            {
                var startUtc = startDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                    : startDate.Value.ToUniversalTime();

                query = query.Where(i => i.CreatedAt >= startUtc);
            }

            if (endDate.HasValue)
            {
                var endUtc = endDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                    : endDate.Value.ToUniversalTime();

                var adjustedEndDate = endUtc.Date.AddDays(1).AddTicks(-1);
                query = query.Where(i => i.CreatedAt <= adjustedEndDate);
            }

            query = sortBy?.ToLower() switch
            {
                "invoicenumber" => ascending ? query.OrderBy(i => i.InvoiceNumber) : query.OrderByDescending(i => i.InvoiceNumber),
                "customer" => ascending ? query.OrderBy(i => i.Quotation != null && i.Quotation.Customer != null ? i.Quotation.Customer.CompanyName : string.Empty) : query.OrderByDescending(i => i.Quotation != null && i.Quotation.Customer != null ? i.Quotation.Customer.CompanyName : string.Empty),
                "issuedate" => ascending ? query.OrderBy(i => i.IssueDate) : query.OrderByDescending(i => i.IssueDate),
                "status" => ascending ? query.OrderBy(i => i.Status) : query.OrderByDescending(i => i.Status),
                "balancedue" => ascending ? query.OrderBy(i => i.BalanceDue) : query.OrderByDescending(i => i.BalanceDue),
                "totalamount" => ascending ? query.OrderBy(i => i.TotalAmount) : query.OrderByDescending(i => i.TotalAmount),
                "createdat" or _ => ascending ? query.OrderBy(i => i.CreatedAt) : query.OrderByDescending(i => i.CreatedAt),
            };

            return await query
                .Select(i => MapToDto(i))
                .ToListAsync();
        }

        public async Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Quotation)
                    .ThenInclude(q => q!.Customer)
                .Include(i => i.Payments)
                .Include(i => i.Items)
                    .ThenInclude(item => item.ProductVariant!)
                        .ThenInclude(v => v!.Product)
                .FirstOrDefaultAsync(i => i.InvoiceId == id && !i.IsDeleted);

            if (invoice == null) return null;
            return MapToDto(invoice);
        }

        public async Task<InvoiceResponseDto> CreateInvoiceFromQuotationAsync(CreateInvoiceFromQuotationDto dto, string userId)
        {
            var existingActiveInvoice = await _context.Invoices
                .AnyAsync(i => i.QuotationId == dto.QuotationId && !i.IsDeleted && i.Status != "Cancelled");

            if (existingActiveInvoice)
                throw new InvalidOperationException("An active invoice has already been generated for this quotation. Please cancel the existing invoice before generating a new one for updated quotations.");

            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.QuotationId == dto.QuotationId && !q.IsDeleted);

            if (quotation == null)
                throw new KeyNotFoundException("Quotation not found.");

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
                    CreatedAt = DateTime.UtcNow,
                    Items = quotation.Items.Select(qi => new InvoiceItem
                    {
                        ProductVariantId = qi.ProductVariantId,
                        Description = qi.Description,
                        Quantity = qi.Quantity,
                        UnitPrice = qi.UnitPrice,
                        TotalAmount = qi.TotalAmount
                    }).ToList()
                };

                quotation.Status = "Accepted";

                _context.Invoices.Add(invoice);

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

        public async Task<bool> DeleteInvoiceAsync(int id)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && !i.IsDeleted);
            if (invoice == null) return false;

            invoice.IsDeleted = true; // Soft delete instead of changing status to Cancelled
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PaymentResponseDto?> RecordPaymentAsync(int invoiceId, RecordPaymentDto dto, string userId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && !i.IsDeleted);

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
            var itemDtos = i.Items?.Select(item => new QuotationItemResponseDto(
                item.InvoiceItemId,
                item.ProductVariantId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.TotalAmount,
                item.ProductVariant?.Product?.Name,
                item.ProductVariant?.SKU,
                item.ProductVariant?.Color,
                item.ProductVariant?.Size
            )).ToList() ?? new List<QuotationItemResponseDto>();

            return new InvoiceResponseDto(
                i.InvoiceId,
                i.InvoiceNumber,
                i.QuotationId ?? 0,
                i.Quotation != null ? i.Quotation.QuotationNumber : string.Empty,
                i.CustomerId,
                i.Quotation?.Customer?.CompanyName ?? string.Empty,
                i.Quotation?.Customer?.CompanyAddress ?? string.Empty,
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
                )).ToList(),
                itemDtos
            );
        }

        public async Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id)
        {
            var invoice = await _context.Invoices
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.InvoiceId == id && !x.IsDeleted);

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

        public async Task<IEnumerable<InvoiceResponseDto>> GetDeletedInvoicesAsync(string? search = null)
        {
            var query = _context.Invoices
                .Include(i => i.Quotation)
                    .ThenInclude(q => q!.Customer)
                .Include(i => i.Payments)
                .Include(i => i.Items)
                    .ThenInclude(item => item.ProductVariant!)
                        .ThenInclude(v => v!.Product)
                .Where(i => i.IsDeleted) // Query actual soft-deleted items
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(lowerSearch) ||
                    (i.Quotation != null && i.Quotation.QuotationNumber.ToLower().Contains(lowerSearch)) || // Added quotation number search
                    (i.Quotation != null && i.Quotation.Customer != null && i.Quotation.Customer.CompanyName.ToLower().Contains(lowerSearch))
                );
            }
            

            return await query
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => MapToDto(i))
                .ToListAsync();
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && !i.IsDeleted);
            if (invoice == null) return false;

            invoice.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RestoreInvoiceAsync(int id)
        {
            var invoice = await _context.Invoices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.IsDeleted);

            if (invoice == null) return false;

            invoice.IsDeleted = false; // Restore from trash
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

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}