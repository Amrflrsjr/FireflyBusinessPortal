using System;
using System.Collections.Generic;
using System.Text;

namespace Firefly.Application.Dashboard.Dtos
{
    public record DashboardChartPointDto(string Date, decimal Amount);
    public record InvoiceStatusBreakdownDto(string Status, int Count, decimal Amount);
    public record AgingBucketDto(string Range, decimal Amount, int Count);
    public record TopCustomerDto(string CustomerName, decimal TotalRevenue);
    public record MonthlyRevenueDto(string Month, decimal Amount);

    public record DashboardMetricsResponseDto(
        decimal TotalRevenue,
        int UnpaidCount,
        int ActiveQuotesCount,
        int AcceptedQuotesCount,
        int TotalCustomersCount,
        int CorporateCustomersCount,
        int PersonalCustomersCount,
        decimal TotalPeriodRevenue,
        IEnumerable<DashboardChartPointDto> ChartData,
        // --- new fields ---
        IEnumerable<InvoiceStatusBreakdownDto> InvoiceStatusBreakdown,
        IEnumerable<AgingBucketDto> AgingBuckets,
        IEnumerable<TopCustomerDto> TopCustomers,
        IEnumerable<MonthlyRevenueDto> MonthlyRevenue
    );
}