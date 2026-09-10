using Firefly.Application.Common.Interfaces;
using Firefly.Application.Search.Dtos;
using Firefly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Infrastructure.Services
{
    public class SearchService : ISearchService
    {
        private readonly ApplicationDbContext _context;

        public SearchService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<GlobalSearchResponseDto> SearchAllAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new GlobalSearchResponseDto([], [], [], []);

            var q = query.ToLower().Trim();

            // 1. Customers (matching company name, TIN, or contact details)
            var customers = await _context.Customers
                .Include(c => c.Contacts)
                .Where(c =>
                    c.CompanyName.ToLower().Contains(q) ||
                    c.TIN.Contains(q) ||
                    c.Contacts.Any(cc =>
                        cc.Name.ToLower().Contains(q) ||
                        cc.Email.ToLower().Contains(q) ||
                        cc.Phone.Contains(q) ||
                        cc.Department.ToLower().Contains(q) ||
                        cc.Position.ToLower().Contains(q)
                    )
                )
                .Take(5)
                .Select(c => new SearchItemDto(
                    c.CustomerId,
                    c.CompanyName,
                    $"TIN: {c.TIN}",
                    "Customer",
                    $"/customers?search={c.CustomerId}"
                ))
                .ToListAsync();

            // 2. Invoices
            var invoices = await _context.Invoices
                .Include(i => i.Customer)
                .Where(i => i.InvoiceNumber.ToLower().Contains(q) || i.Customer.CompanyName.ToLower().Contains(q))
                .Take(5)
                .Select(i => new SearchItemDto(
                    i.InvoiceId,
                    i.InvoiceNumber,
                    $"{i.Customer.CompanyName} • Due: PHP {i.BalanceDue:N2}",
                    "Invoice",
                    $"/invoices?search={i.InvoiceNumber}"
                ))
                .ToListAsync();

            // 3. Quotations
            var quotations = await _context.Quotations
                .Include(qt => qt.Customer)
                .Where(qt => qt.QuotationNumber.ToLower().Contains(q) || qt.Customer.CompanyName.ToLower().Contains(q))
                .Take(5)
                .Select(qt => new SearchItemDto(
                    qt.QuotationId,
                    qt.QuotationNumber,
                    $"{qt.Customer.CompanyName} • Status: {qt.Status}",
                    "Quotation",
                    $"/quotations?search={qt.QuotationNumber}"
                ))
                .ToListAsync();

            // 4. Products (matching product name, description, or variant SKU, color, size)
            var products = await _context.Products
                .Include(p => p.Variants)
                .Where(p => !p.IsDeleted && (
                    p.Name.ToLower().Contains(q) ||
                    p.Description.ToLower().Contains(q) ||
                    p.Variants.Any(v =>
                        v.SKU.ToLower().Contains(q) ||
                        v.Color.ToLower().Contains(q) ||
                        v.Size.ToLower().Contains(q)
                    )
                ))
                .Take(5)
                .Select(p => new SearchItemDto(
                    p.ProductId,
                    p.Name,
                    p.Variants.Any() ? $"{p.Variants.Count} variant(s) available" : p.Description,
                    "Product",
                    $"/products?search={p.Name}"
                ))
                .ToListAsync();

            return new GlobalSearchResponseDto(customers, quotations, invoices, products);
        }
    }
}