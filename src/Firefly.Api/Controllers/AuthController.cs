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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
        }

        public record LoginRequest(string Username, string Password);
        public record LoginResponse(string Token, string UserId, string Username, IEnumerable<string> Roles);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Username and password are required" });

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
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Debug log to container console
            var userClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
            Console.WriteLine($"[AUTH DEBUG] User Claims: {string.Join(" | ", userClaims)}");

            // 1. Basic Field Validations
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Username and password are required" });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email address is required" });

            // 2. Normalize and check for existing Username
            var normalizedUsername = request.Username.Trim();
            var userExists = await _userManager.FindByNameAsync(normalizedUsername);
            if (userExists != null)
                return BadRequest(new { message = "Username is already taken" });

            // 3. Normalize and check for existing Email (Case-Insensitive)
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var emailExists = await _userManager.FindByEmailAsync(normalizedEmail);
            if (emailExists != null)
                return BadRequest(new { message = "Email address is already in use" });

            var user = new ApplicationUser
            {
                UserName = normalizedUsername,
                Email = normalizedEmail,
                FullName = request.FullName?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "User creation failed", errors });
            }

            // 4. Ensure requested role exists in identity database before assigning
            var roleToAssign = string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role.Trim();
            if (!await _roleManager.RoleExistsAsync(roleToAssign))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleToAssign));
            }

            await _userManager.AddToRoleAsync(user, roleToAssign);

            return Ok(new { message = $"User registered successfully with role '{roleToAssign}'" });
        }
    }
}