using Firefly.Application.Auth.Dtos;
using Firefly.Application.Common.Interfaces;
using Firefly.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Firefly.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;

        public AuthController(UserManager<ApplicationUser> userManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        public record LoginRequest(string Username, string Password);
        public record LoginResponse(string Token, string UserId, string Username, IEnumerable<string> Roles);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null || !user.IsActive)
                return Unauthorized(new { message = "Invalid username or password" });

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid)
                return Unauthorized(new { message = "Invalid username or password" });

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateJwtToken(user.Id, user.UserName!, user.Email!, roles);

            return Ok(new LoginResponse(token, user.Id, user.UserName!, roles));
        }

        [HttpPost("register")]
        [Authorize]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var userExists = await _userManager.FindByNameAsync(request.Username);
            if (userExists != null)
                return BadRequest(new { message = "Username is already taken" });

            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                FullName = request.FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "User creation failed", errors });
            }

            return Ok(new { message = "User registered successfully" });
        }
    }
}