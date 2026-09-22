using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Firefly.Application.Common.Interfaces;
using Firefly.Application.Dashboard.Dtos;
using Firefly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Application.Dashboard.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        // How many trailing calendar months to include in the monthly revenue series.
        private const int MonthlyRevenueMonthsBack = 12;

        // How many customers to surface in the top-customers chart.
        private const int TopCustomersLimit = 10;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardMetricsResponseDto> GetDashboardMetricsAsync(string timeRange = "30d")
        {
            // Exclude cancelled/deleted invoices and quotations via Status check
            var totalRevenue = await _context.Invoices
                .Where(i => i.Status.ToLower() != "cancelled" && i.Status.ToLower() == "paid")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var unpaidCount = await _context.Invoices
                .CountAsync(i => i.Status.ToLower() != "cancelled" && (i.Status.ToLower() == "unpaid" || i.Status.ToLower() == "partiallypaid"));

            var activeQuotesCount = await _context.Quotations
                .CountAsync(q => q.Status.ToLower() != "cancelled" && q.Status.ToLower() != "declined" && (q.Status.ToLower() == "created" || q.Status.ToLower() == "sent"));

            var acceptedQuotesCount = await _context.Quotations
                .CountAsync(q => q.Status.ToLower() != "cancelled" && (q.Status.ToLower() == "accepted" || q.Status.ToLower() == "approved"));

            var totalCustomersCount = await _context.Customers
                .CountAsync(c => c.IsActive);

            // Safe null checks added to prevent exceptions if CustomerType is null in the database
            var personalCustomersCount = await _context.Customers
                .CountAsync(c => c.IsActive && c.CustomerType != null && c.CustomerType.ToLower() == "individual");

            var corporateCustomersCount = await _context.Customers
                .CountAsync(c => c.IsActive && c.CustomerType != null && c.CustomerType.ToLower() != "individual");

            // 2. Handle Date Range Cutoff for Payments / Income Trend
            DateTime? cutoffDate = timeRange switch
            {
                "7d" => DateTime.UtcNow.AddDays(-7),
                "30d" => DateTime.UtcNow.AddDays(-30),
                "90d" => DateTime.UtcNow.AddDays(-90),
                _ => null
            };

            var paymentQuery = _context.Payments
                .Include(p => p.Invoice)
                .Where(p => p.Invoice.Status.ToLower() != "cancelled");

            if (cutoffDate.HasValue)
            {
                paymentQuery = paymentQuery.Where(p => p.CreatedAt >= cutoffDate.Value);
            }

            var rawPayments = await paymentQuery
                .Select(p => new { p.CreatedAt, p.AmountPaid })
                .ToListAsync();

            // 3. Group by formatted date in memory
            var groupedChartData = rawPayments
                .GroupBy(p => p.CreatedAt.ToString("MMM dd" + (timeRange == "all" || timeRange == "90d" ? ", yy" : "")))
                .Select(g => new DashboardChartPointDto(
                    g.Key,
                    g.Sum(x => x.AmountPaid)
                ))
                .OrderBy(x => DateTime.Parse(x.Date))
                .ToList();

            var totalPeriodRevenue = groupedChartData.Sum(x => x.Amount);

            // 4. Invoice status breakdown (Paid / Unpaid / Overdue)
            var invoiceStatusBreakdown = await GetInvoiceStatusBreakdownAsync();

            // 5. Receivables aging buckets (0-30 / 31-60 / 61-90 / 90+)
            var agingBuckets = await GetAgingBucketsAsync();

            // 6. Top customers by paid revenue
            var topCustomers = await GetTopCustomersAsync();

            // 7. Monthly revenue (trailing calendar months, distinct from the rolling ChartData above)
            var monthlyRevenue = await GetMonthlyRevenueAsync();

            return new DashboardMetricsResponseDto(
                TotalRevenue: totalRevenue,
                UnpaidCount: unpaidCount,
                ActiveQuotesCount: activeQuotesCount,
                AcceptedQuotesCount: acceptedQuotesCount,
                TotalCustomersCount: totalCustomersCount,
                CorporateCustomersCount: corporateCustomersCount,
                PersonalCustomersCount: personalCustomersCount,
                TotalPeriodRevenue: totalPeriodRevenue,
                ChartData: groupedChartData,
                InvoiceStatusBreakdown: invoiceStatusBreakdown,
                AgingBuckets: agingBuckets,
                TopCustomers: topCustomers,
                MonthlyRevenue: monthlyRevenue
            );
        }

        // ---------------------------------------------------------------
        // Invoice status breakdown: Paid / Unpaid / Overdue
        // "Overdue" isn't a stored Status value — it's Unpaid/PartiallyPaid
        // with DueDate in the past, so it has to be derived here.
        // ---------------------------------------------------------------
        private async Task<List<InvoiceStatusBreakdownDto>> GetInvoiceStatusBreakdownAsync()
        {
            var today = DateTime.UtcNow.Date;

            var paid = await _context.Invoices
                .Where(i => !i.IsDeleted && i.Status.ToLower() == "paid")
                .GroupBy(i => 1)
                .Select(g => new { Count = g.Count(), Amount = g.Sum(i => i.TotalAmount) })
                .FirstOrDefaultAsync();

            var outstanding = await _context.Invoices
                .Where(i => !i.IsDeleted &&
                            (i.Status.ToLower() == "unpaid" || i.Status.ToLower() == "partiallypaid"))
                .Select(i => new { i.DueDate, i.BalanceDue })
                .ToListAsync();

            var overdue = outstanding.Where(i => i.DueDate.Date < today).ToList();
            var notYetDue = outstanding.Where(i => i.DueDate.Date >= today).ToList();

            return new List<InvoiceStatusBreakdownDto>
            {
                new("Paid", paid?.Count ?? 0, paid?.Amount ?? 0m),
                new("Unpaid", notYetDue.Count, notYetDue.Sum(i => i.BalanceDue)),
                new("Overdue", overdue.Count, overdue.Sum(i => i.BalanceDue))
            };
        }

        // ---------------------------------------------------------------
        // Receivables aging: bucketed by days past DueDate, on outstanding
        // BalanceDue for non-paid, non-cancelled, non-deleted invoices.
        // Invoices not yet due are folded into the "0-30" bucket.
        // ---------------------------------------------------------------
        private async Task<List<AgingBucketDto>> GetAgingBucketsAsync()
        {
            var today = DateTime.UtcNow.Date;

            var outstanding = await _context.Invoices
                .Where(i => !i.IsDeleted &&
                            (i.Status.ToLower() == "unpaid" || i.Status.ToLower() == "partiallypaid"))
                .Select(i => new { i.DueDate, i.BalanceDue })
                .ToListAsync();

            var results = new List<AgingBucketDto>
            {
                new("0-30", 0m, 0),
                new("31-60", 0m, 0),
                new("61-90", 0m, 0),
                new("90+", 0m, 0)
            };

            foreach (var invoice in outstanding)
            {
                var daysPastDue = Math.Max(0, (today - invoice.DueDate.Date).Days);

                var index = daysPastDue switch
                {
                    <= 30 => 0,
                    <= 60 => 1,
                    <= 90 => 2,
                    _ => 3
                };

                var current = results[index];
                results[index] = current with
                {
                    Amount = current.Amount + invoice.BalanceDue,
                    Count = current.Count + 1
                };
            }

            return results;
        }

        // ---------------------------------------------------------------
        // Top customers by realized (Paid) revenue.
        // ---------------------------------------------------------------
        private async Task<List<TopCustomerDto>> GetTopCustomersAsync()
        {
            // Project to a flat, translatable shape first (EF Core can't translate
            // GroupBy + Sum combined with a navigation property in the key), then
            // group/sort in memory, same pattern as rawPayments above.
            var rawPaidInvoices = await _context.Invoices
                .Where(i => !i.IsDeleted && i.Status.ToLower() == "paid" && i.Customer != null)
                .Select(i => new { i.CustomerId, CompanyName = i.Customer!.CompanyName, i.TotalAmount })
                .ToListAsync();

            return rawPaidInvoices
                .GroupBy(i => new { i.CustomerId, i.CompanyName })
                .Select(g => new TopCustomerDto(
                    g.Key.CompanyName,
                    g.Sum(i => i.TotalAmount)
                ))
                .OrderByDescending(c => c.TotalRevenue)
                .Take(TopCustomersLimit)
                .ToList();
        }

        // ---------------------------------------------------------------
        // Monthly revenue: trailing calendar months of actual payments
        // received (PaymentDate), independent of the timeRange param.
        // ---------------------------------------------------------------
        private async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync()
        {
            var startDate = DateTime.UtcNow.Date.AddMonths(-(MonthlyRevenueMonthsBack - 1));
            startDate = DateTime.SpecifyKind(new DateTime(startDate.Year, startDate.Month, 1), DateTimeKind.Utc);

            var rawPayments = await _context.Payments
                .Include(p => p.Invoice)
                .Where(p => p.Invoice.Status.ToLower() != "cancelled" && p.PaymentDate >= startDate)
                .Select(p => new { p.PaymentDate, p.AmountPaid })
                .ToListAsync();

            // Build the full trailing window up front so months with no payments still show as 0.
            var months = Enumerable.Range(0, MonthlyRevenueMonthsBack)
                .Select(offset => startDate.AddMonths(offset))
                .ToList();

            var totalsByMonth = rawPayments
                .GroupBy(p => new DateTime(p.PaymentDate.Year, p.PaymentDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));

            return months
                .Select(m => new MonthlyRevenueDto(
                    m.ToString("MMM yyyy"),
                    totalsByMonth.TryGetValue(m, out var amount) ? amount : 0m
                ))
                .ToList();
        }
    }
}