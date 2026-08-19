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

        public async Task<IEnumerable<QuotationResponseDto>> GetAllQuotationsAsync()
        {
            return await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Items)
                .Where(q => q.Status != "Cancelled") // Filter out soft-deleted/cancelled quotations[cite: 21]
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => MapToDto(q))
                .ToListAsync();
        }

        public async Task<QuotationResponseDto?> GetQuotationByIdAsync(int id)
        {
            var q = await _context.Quotations
                .Include(x => x.Customer)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.QuotationId == id && x.Status != "Cancelled"); // Ensure active quotation[cite: 21]

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

            var todayPrefix = $"QT-{DateTime.UtcNow:yyyyMMdd}";
            var countToday = await _context.Quotations
                .CountAsync(q => q.QuotationNumber.StartsWith(todayPrefix));
            var quotationNumber = $"{todayPrefix}-{(countToday + 1):D4}";

            var quotation = new Quotation
            {
                QuotationNumber = quotationNumber,
                CustomerId = dto.CustomerId,
                ContactId = contact?.ContactId ?? (dto.ContactId.HasValue && dto.ContactId.Value > 0 ? dto.ContactId : null),
                ContactNameSnapshot = contactName,
                ContactEmailSnapshot = contactEmail,
                ContactPositionSnapshot = contactPosition,
                DateGenerated = DateTime.UtcNow,
                ValidUntil = dto.ValidUntil,
                VATType = dto.VATType,
                Status = "Created",
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

        public async Task<bool> UpdateStatusAsync(int id, UpdateQuotationStatusDto dto)
        {
            var quotation = await _context.Quotations.FirstOrDefaultAsync(q => q.QuotationId == id && q.Status != "Cancelled");
            if (quotation == null) return false;

            quotation.Status = dto.Status;
            await _context.SaveChangesAsync();
            return true;
        }

        // Soft delete implementation via status update to Cancelled[cite: 21]
        public async Task<bool> DeleteQuotationAsync(int id)
        {
            var quotation = await _context.Quotations.FirstOrDefaultAsync(q => q.QuotationId == id && q.Status != "Cancelled");
            if (quotation == null) return false;

            quotation.Status = "Cancelled";
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
                    i.TotalAmount
                )).ToList()
            );
        }

        public async Task<DocumentEmailPreviewDto?> GetEmailPreviewAsync(int id)
        {
            var q = await _context.Quotations
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.QuotationId == id && x.Status != "Cancelled"); //[cite: 21]

            if (q == null) return null;

            var settings = await _context.CompanySettings.FirstOrDefaultAsync();

            string subject = !string.IsNullOrWhiteSpace(settings?.PaymentOptions)
                ? $"Quotation #{q.QuotationNumber} - {q.Customer?.CompanyName}"
                : $"Quotation #{q.QuotationNumber}";
            string pdfFileName = $"Quotation_{q.QuotationNumber}.pdf";

            string body = "";

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
    }
}