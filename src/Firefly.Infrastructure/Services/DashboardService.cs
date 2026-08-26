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

            return new DashboardMetricsResponseDto(
                TotalRevenue: totalRevenue,
                UnpaidCount: unpaidCount,
                ActiveQuotesCount: activeQuotesCount,
                AcceptedQuotesCount: acceptedQuotesCount,
                TotalCustomersCount: totalCustomersCount,
                CorporateCustomersCount: corporateCustomersCount,
                PersonalCustomersCount: personalCustomersCount,
                TotalPeriodRevenue: totalPeriodRevenue,
                ChartData: groupedChartData
            );
        }
    }
}