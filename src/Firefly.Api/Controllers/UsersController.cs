using System.Security.Claims;
using Firefly.Application.Users.Dtos;
using Firefly.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "Invalid token claims." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new UserResponseDto(
                user.Id,
                user.UserName!,
                user.Email!,
                user.FullName,
                user.ProfilePictureUrl ?? string.Empty,
                user.IsActive,
                roles,
                user.CreatedAt
            ));
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateCurrentUser([FromForm] UpdateUserDto dto, IFormFile? profilePicture)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "Invalid token claims." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });

            user.FullName = dto.FullName?.Trim() ?? user.FullName;
            user.Email = dto.Email?.Trim().ToLowerInvariant() ?? user.Email;

            if (profilePicture != null && profilePicture.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(profilePicture.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }

                user.ProfilePictureUrl = $"/uploads/avatars/{uniqueFileName}";
            }
            else if (!string.IsNullOrEmpty(dto.ProfilePictureUrl))
            {
                user.ProfilePictureUrl = dto.ProfilePictureUrl;
            }

            user.UpdatedAt = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(new { message = "Failed to update profile", errors = updateResult.Errors.Select(e => e.Description) });

            return Ok(new { message = "Profile updated successfully", profilePictureUrl = user.ProfilePictureUrl });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<UserResponseDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserResponseDto(
                    user.Id,
                    user.UserName!,
                    user.Email!,
                    user.FullName,
                    user.ProfilePictureUrl ?? string.Empty,
                    user.IsActive,
                    roles,
                    user.CreatedAt
                ));
            }

            return Ok(userList);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new UserResponseDto(
                user.Id,
                user.UserName!,
                user.Email!,
                user.FullName,
                user.ProfilePictureUrl ?? string.Empty,
                user.IsActive,
                roles,
                user.CreatedAt
            ));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            var user = new ApplicationUser
            {
                UserName = dto.Username.Trim(),
                Email = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty,
                FullName = dto.FullName?.Trim() ?? string.Empty,
                ProfilePictureUrl = dto.ProfilePictureUrl?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = "Failed to create user", errors = result.Errors.Select(e => e.Description) });

            var role = string.IsNullOrWhiteSpace(dto.Role) ? "Staff" : dto.Role.Trim();
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }

            await _userManager.AddToRoleAsync(user, role);
            return Ok(new { message = "User created successfully" });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });

            user.FullName = dto.FullName?.Trim() ?? user.FullName;
            user.Email = dto.Email?.Trim().ToLowerInvariant() ?? user.Email;
            user.ProfilePictureUrl = dto.ProfilePictureUrl?.Trim() ?? user.ProfilePictureUrl;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(new { message = "Failed to update user", errors = updateResult.Errors.Select(e => e.Description) });

            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

                if (!await _roleManager.RoleExistsAsync(dto.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(dto.Role));
                }
                await _userManager.AddToRoleAsync(user, dto.Role);
            }

            return Ok(new { message = "User updated successfully" });
        }

        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                return BadRequest(new { message = "New password must be at least 8 characters long." });

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && currentUserId != id)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are only allowed to reset your own password." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(new { message = "Password reset failed", errors = result.Errors.Select(e => e.Description) });

            return Ok(new { message = "Password reset successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "User deactivated successfully." });
        }
    }
}