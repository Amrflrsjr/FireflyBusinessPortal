using Firefly.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet]
        public async Task<IActionResult> GlobalSearch([FromQuery] string q)
        {
            // E.g. GET /api/search?q=netflix
            var results = await _searchService.SearchAllAsync(q);
            return Ok(results);
        }
    }
}