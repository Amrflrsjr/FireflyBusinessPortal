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
                return new GlobalSearchResponseDto([], [], []);

            var q = query.ToLower().Trim();

            // 1. Await the Customer Query immediately
            var customers = await _context.Customers
                .Where(c => c.CompanyName.ToLower().Contains(q) || c.TIN.Contains(q))
                .Take(5)
                .Select(c => new SearchItemDto(
                    c.CustomerId,
                    c.CompanyName,
                    $"TIN: {c.TIN}",
                    "Customer",
                    $"/customers?search={c.CustomerId}"
                ))
                .ToListAsync();

            // 2. Await the Invoice Query immediately
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

            // 3. Await the Quotation Query immediately
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

            // 4. Return the results
            return new GlobalSearchResponseDto(customers, quotations, invoices);
        }
    }
}