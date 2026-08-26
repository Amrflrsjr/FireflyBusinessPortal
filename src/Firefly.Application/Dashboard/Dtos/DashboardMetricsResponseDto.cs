using System;
using System.Collections.Generic;
using System.Text;

namespace Firefly.Application.Dashboard.Dtos
{
    public record DashboardChartPointDto(string Date, decimal Amount);

    public record DashboardMetricsResponseDto(
        decimal TotalRevenue,
        int UnpaidCount,
        int ActiveQuotesCount,
        int AcceptedQuotesCount,
        int TotalCustomersCount,
        int CorporateCustomersCount,
        int PersonalCustomersCount,
        decimal TotalPeriodRevenue,
        IEnumerable<DashboardChartPointDto> ChartData
    );
}