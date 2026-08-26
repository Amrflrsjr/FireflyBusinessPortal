using System;
using System.Collections.Generic;
using System.Text;
using Firefly.Application.Dashboard.Dtos;

namespace Firefly.Application.Common.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardMetricsResponseDto> GetDashboardMetricsAsync(string timeRange = "30d");
    }
}