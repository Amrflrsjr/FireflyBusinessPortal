using Microsoft.AspNetCore.Mvc;
using Firefly.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] string timeRange = "30d")
        {
            var metrics = await _dashboardService.GetDashboardMetricsAsync(timeRange);
            return Ok(metrics);
        }
    }
}