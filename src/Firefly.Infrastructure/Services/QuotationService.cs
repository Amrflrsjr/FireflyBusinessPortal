using Firefly.Application.Common.Interfaces;
using Firefly.Application.Quotations.Dtos;
using Firefly.Domain.Entities;
using Firefly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Infrastructure.Services
{
    public class QuotationService : IQuotationService
    {
        private readonly ApplicationDbContext _context;

        public QuotationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<QuotationResponseDto>> GetAllQuotationsAsync(
         string? search = null,
         string? status = null,
         DateTime? startDate = null,
         DateTime? endDate = null,
         string? sortBy = null,
         bool ascending = true)
        {
            var query = _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.Items)
                .ThenInclude(i => i.ProductVariant!)
                    .ThenInclude(v => v.Product)
            .Where(q => !q.IsDeleted) // Decoupled: filters out soft-deleted items instead of status[cite: 11]
            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(q =>
                    q.QuotationNumber.ToLower().Contains(search) ||
                    (q.Customer != null && q.Customer.CompanyName.ToLower().Contains(search)) ||
                    (!string.IsNullOrEmpty(q.ContactNameSnapshot) && q.ContactNameSnapshot.ToLower().Contains(search))
                );
            }

            // Backend Status Filter
            if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
            {
                query = query.Where(q => q.Status.ToLower() == status.ToLower());
            }

            // Backend Date Range Filters with UTC Kind adjustment
            if (startDate.HasValue)
            {
                var startUtc = startDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                    : startDate.Value.ToUniversalTime();

                query = query.Where(q => q.CreatedAt >= startUtc);
            }

            if (endDate.HasValue)
            {
                var endUtc = endDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                    : endDate.Value.ToUniversalTime();

                var adjustedEndDate = endUtc.Date.AddDays(1).AddTicks(-1);
                query = query.Where(q => q.CreatedAt <= adjustedEndDate);
            }

            query = sortBy?.ToLower() switch
            {
                "quotationnumber" => ascending ? query.OrderBy(q => q.QuotationNumber) : query.OrderByDescending(q => q.QuotationNumber),
                "customer" => ascending ? query.OrderBy(q => q.Customer != null ? q.Customer.CompanyName : string.Empty) : query.OrderByDescending(q => q.Customer != null ? q.Customer.CompanyName : string.Empty),
                "totalamount" => ascending ? query.OrderBy(q => q.TotalAmount) : query.OrderByDescending(q => q.TotalAmount),
                "status" => ascending ? query.OrderBy(q => q.Status) : query.OrderByDescending(q => q.Status),
                "createdat" or _ => ascending ? query.OrderBy(q => q.CreatedAt) : query.OrderByDescending(q => q.CreatedAt),
            };

            return await query
                .Select(q => MapToDto(q))
                .ToListAsync();
        }

        public async Task<QuotationResponseDto?> GetQuotationByIdAsync(int id)
        {
            var q = await _context.Quotations
            .Include(x => x.Customer)
            .Include(x => x.Items)
                .ThenInclude(i => i.ProductVariant!)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(x => x.QuotationId == id && !x.IsDeleted);

            if (q == null) return null;
            return MapToDto(q);
        }

        public async Task<QuotationResponseDto> CreateQuotationAsync(CreateQuotationDto dto, string userId)
        {
            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.CustomerId == dto.CustomerId && c.IsActive);

            if (customer == null)
                throw new KeyNotFoundException("Customer not found.");

            CustomerContact? contact = null;
            if (dto.ContactId.HasValue && dto.ContactId.Value > 0)
            {
                contact = customer.Contacts.FirstOrDefault(c => c.ContactId == dto.ContactId.Value && c.IsActive);
            }

            if (contact == null)
            {
                contact = customer.Contacts.FirstOrDefault(c => c.IsPrimary && c.IsActive)
                       ?? customer.Contacts.FirstOrDefault(c => c.IsActive);
            }

            string contactName = contact?.Name ?? string.Empty;
            string contactEmail = contact?.Email ?? string.Empty;
            string contactPosition = contact?.Position ?? string.Empty;

            decimal rawTotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
            decimal subtotal = 0;
            decimal vatAmount = 0;
            decimal totalAmount = 0;

            const decimal vatRate = 0.12m;
            string normalizedVatType = dto.VATType?.Trim() ?? "Exclusive";

            switch (normalizedVatType)
            {
                case "Inclusive":
                case "VAT Inclusive":
                    totalAmount = rawTotal;
                    subtotal = Math.Round(rawTotal / (1 + vatRate), 2);
                    vatAmount = totalAmount - subtotal;
                    break;

                case "Exclusive":
                case "VAT Exclusive":
                    subtotal = rawTotal;
                    vatAmount = Math.Round(rawTotal * vatRate, 2);
                    totalAmount = subtotal + vatAmount;
                    break;

                case "ZeroRated":
                case "Zero Rated":
                case "VAT Exempt":
                case "OutOfScope":
                default:
                    subtotal = rawTotal;
                    vatAmount = 0;
                    totalAmount = rawTotal;
                    break;
            }

            var monthYearPrefix = DateTime.UtcNow.ToString("MMyy");
            var countToday = await _context.Quotations
                .CountAsync(q => q.QuotationNumber.StartsWith(monthYearPrefix));
            var quotationNumber = $"{monthYearPrefix}-{(countToday + 1):D5}";

            var quotation = new Quotation
            {
                QuotationNumber = quotationNumber,
                CustomerId = dto.CustomerId,
                ContactId = contact?.ContactId ?? (dto.ContactId.HasValue && dto.ContactId.Value > 0 ? dto.ContactId : null),
                ContactNameSnapshot = contactName,
                ContactEmailSnapshot = contactEmail,
                ContactPositionSnapshot = contactPosition,
                DateGenerated = DateTime.UtcNow,
                ValidUntil = dto.ValidUntil != default ? dto.ValidUntil : DateTime.UtcNow.AddDays(7),
                VATType = dto.VATType ?? "Exclusive",
                Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status : "Created",
                NoteToCustomer = dto.NoteToCustomer,
                PreparedByFK = userId,
                Subtotal = subtotal,
                VATAmount = vatAmount,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in dto.Items)
            {
                quotation.Items.Add(new QuotationItem
                {
                    ProductVariantId = item.ProductVariantId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalAmount = item.Quantity * item.UnitPrice
                });
            }

            _context.Quotations.Add(quotation);
            await _context.SaveChangesAsync();

            return (await GetQuotationByIdAsync(quotation.QuotationId))!;
        }

        public async Task<bool> UpdateQuotationAsync(int id, UpdateQuotationDto dto, string userId)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .Include(q => q.Customer)
                .ThenInclude(c => c!.Contacts)
                .FirstOrDefaultAsync(q => q.QuotationId == id && !q.IsDeleted);

            if (quotation == null) return false;

            if (quotation.Status != "Created" && quotation.Status != "Draft")
            {
                throw new InvalidOperationException("Only quotations in Created or Draft status can be edited.");
            }

            var customer = await _context.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.CustomerId == dto.CustomerId && c.IsActive);

            if (customer == null)
                throw new KeyNotFoundException("Customer not found.");

            CustomerContact? contact = null;
            if (dto.ContactId.HasValue && dto.ContactId.Value > 0)
            {
                contact = customer.Contacts.FirstOrDefault(c => c.ContactId == dto.ContactId.Value && c.IsActive);
            }

            if (contact == null)
            {
                contact = customer.Contacts.FirstOrDefault(c => c.IsActive && c.IsPrimary)
                       ?? customer.Contacts.FirstOrDefault(c => c.IsActive);
            }

            string contactName = contact?.Name ?? string.Empty;
            string contactEmail = contact?.Email ?? string.Empty;
            string contactPosition = contact?.Position ?? string.Empty;

            decimal rawTotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
            decimal subtotal = 0;
            decimal vatAmount = 0;
            decimal totalAmount = 0;

            const decimal vatRate = 0.12m;
            string normalizedVatType = dto.VATType?.Trim() ?? "Exclusive";

            switch (normalizedVatType)
            {
                case "Inclusive":
                case "VAT Inclusive":
                    totalAmount = rawTotal;
                    subtotal = Math.Round(rawTotal / (1 + vatRate), 2);
                    vatAmount = totalAmount - subtotal;
                    break;

                case "Exclusive":
                case "VAT Exclusive":
                    subtotal = rawTotal;
                    vatAmount = Math.Round(rawTotal * vatRate, 2);
                    totalAmount = subtotal + vatAmount;
                    break;

                case "ZeroRated":
                case "Zero Rated":
                case "VAT Exempt":
                case "OutOfScope":
                default:
                    subtotal = rawTotal;
                    vatAmount = 0;
                    totalAmount = rawTotal;
                    break;
            }

            quotation.CustomerId = dto.CustomerId;
            quotation.ContactId = contact?.ContactId ?? (dto.ContactId.HasValue && dto.ContactId.Value > 0 ? dto.ContactId : null);
            quotation.ContactNameSnapshot = contactName;
            quotation.ContactEmailSnapshot = contactEmail;
            quotation.ContactPositionSnapshot = contactPosition;
            quotation.ValidUntil = dto.ValidUntil;
            quotation.VATType = dto.VATType ?? "Exclusive";
            quotation.NoteToCustomer = dto.NoteToCustomer;
            quotation.Subtotal = subtotal;
            quotation.VATAmount = vatAmount;
            quotation.TotalAmount = totalAmount;

            _context.QuotationItems.RemoveRange(quotation.Items);
            quotation.Items.Clear();

            foreach (var item in dto.Items)
            {
                quotation.Items.Add(new QuotationItem
                {
                    ProductVariantId = item.ProductVariantId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalAmount = item.Quantity * item.UnitPrice
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int id, UpdateQuotationStatusDto dto)
        {
            var quotation = await _context.Quotations.FirstOrDefaultAsync(q => q.QuotationId == id && !q.IsDeleted);
            if (quotation == null) return false;

            quotation.Status = dto.Status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteQuotationAsync(int id)
        {
            var quotation = await _context.Quotations.FirstOrDefaultAsync(q => q.QuotationId == id && !q.IsDeleted);
            if (quotation == null) return false;

            quotation.IsDeleted = true; // Soft delete instead of changing status to Cancelled[cite: 11]
            await _context.SaveChangesAsync();
            return true;
        }

        private static QuotationResponseDto MapToDto(Quotation q)
        {
            return new QuotationResponseDto(
                q.QuotationId,
                q.QuotationNumber,
                q.CustomerId,
                q.Customer != null ? q.Customer.CompanyName : string.Empty,
                q.Customer?.CompanyAddress ?? string.Empty,
                q.Customer?.TIN ?? string.Empty,
                q.ContactId,
                q.ContactNameSnapshot,
                q.ContactEmailSnapshot,
                q.ContactPositionSnapshot,
                q.DateGenerated,
                q.ValidUntil,
                q.VATType,
                q.Status,
                q.NoteToCustomer,
                q.Subtotal,
                q.VATAmount,
                q.TotalAmount,
                q.CreatedAt,
                q.Items.Select(i => new QuotationItemResponseDto(
                    i.QuotationItemId,
                    i.ProductVariantId,
                    i.Description,
                    i.Quantity,
                    i.UnitPrice,
                    i.TotalAmount,
                    i.ProductVariant != null && i.ProductVariant.Product != null ? i.ProductVariant.Product.Name : null,
                    i.ProductVariant != null ? i.ProductVariant.SKU : null,
                    i.ProductVariant != null ? i.ProductVariant.Color : null,
                    i.ProductVariant != null ? i.ProductVariant.Size : null
                )).ToList()
            );
        }

        public async Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id)
        {
            var q = await _context.Quotations
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.QuotationId == id && !x.IsDeleted);

            if (q == null) return null;

            var settings = await _context.CompanySettings.FirstOrDefaultAsync();

            string subject = !string.IsNullOrWhiteSpace(settings?.PaymentOptions)
                ? $"Quotation #{q.QuotationNumber} - {q.Customer?.CompanyName}"
                : $"Quotation #{q.QuotationNumber}";
            string pdfFileName = $"Quotation_#{q.QuotationNumber}.pdf";

            string body = string.Empty;

            var recipients = new List<string>();
            if (!string.IsNullOrEmpty(q.ContactEmailSnapshot))
            {
                recipients.Add(q.ContactEmailSnapshot);
            }

            return new DocumentEmailPreviewDto(
                q.QuotationId,
                q.QuotationNumber,
                recipients,
                subject,
                body,
                q.Customer?.CompanyName ?? string.Empty,
                q.ContactNameSnapshot,
                q.TotalAmount,
                pdfFileName
            );
        }

        public async Task<IEnumerable<QuotationResponseDto>> GetDeletedQuotationsAsync(string? search = null)
        {
            var query = _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Items)
                    .ThenInclude(i => i.ProductVariant!)
                        .ThenInclude(v => v.Product)
                .Where(q => q.IsDeleted) // Query actual soft-deleted items[cite: 11]
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(q =>
                    q.QuotationNumber.ToLower().Contains(search) ||
                    (q.Customer != null && q.Customer.CompanyName.ToLower().Contains(search))
                );
            }

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => MapToDto(q))
                .ToListAsync();
        }

        public async Task<bool> RestoreQuotationAsync(int id)
        {
            var quotation = await _context.Quotations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(q => q.QuotationId == id && q.IsDeleted);

            if (quotation == null) return false;

            quotation.IsDeleted = false; // Restore from trash[cite: 11]
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PermanentlyDeleteQuotationAsync(int id)
        {
            var hasInvoice = await _context.Invoices
                .IgnoreQueryFilters()
                .AnyAsync(i => i.QuotationId == id);

            if (hasInvoice)
            {
                throw new InvalidOperationException("Cannot permanently delete this quotation because an invoice has already been generated from it. Please delete or cancel the associated invoice first.");
            }

            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(q => q.QuotationId == id);

            if (quotation == null) return false;

            _context.Quotations.Remove(quotation);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}