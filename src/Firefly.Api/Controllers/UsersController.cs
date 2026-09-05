using System.Security.Claims;
using Firefly.Application.Users.Dtos;
using Firefly.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;

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
        public async Task<IActionResult> GetCurrentUser([FromServices] IConfiguration configuration)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "Invalid token claims." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });

            string pictureUrl = user.ProfilePictureUrl ?? string.Empty;

            // If a profile picture path exists, generate a temporary pre-signed URL valid for 2 hours
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                var awsAccessKey = configuration["AWS:AccessKey"];
                var awsSecretKey = configuration["AWS:SecretKey"];
                var bucketName = configuration["AWS:BucketName"];
                var regionName = configuration["AWS:Region"];

                try
                {
                    var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, Amazon.RegionEndpoint.GetBySystemName(regionName));

                    // Normalize the object key (strip out any full domain prefix if previously stored)
                    var objectKey = user.ProfilePictureUrl.StartsWith("http")
                        ? new Uri(user.ProfilePictureUrl).AbsolutePath.TrimStart('/')
                        : user.ProfilePictureUrl.TrimStart('/');

                    var request = new Amazon.S3.Model.GetPreSignedUrlRequest
                    {
                        BucketName = bucketName,
                        Key = objectKey,
                        Expires = DateTime.UtcNow.AddHours(2)
                    };

                    pictureUrl = s3Client.GetPreSignedURL(request);
                }
                catch
                {
                    // Fallback to stored string if pre-signing fails
                    pictureUrl = user.ProfilePictureUrl;
                }
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new UserResponseDto(
                user.Id,
                user.UserName!,
                user.Email!,
                user.FullName,
                pictureUrl,
                user.IsActive,
                roles,
                user.CreatedAt
            ));
        }

        [HttpPut("me")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateCurrentUser(
            [FromForm] UpdateUserDto dto,
            IFormFile? profilePicture,
            [FromServices] IConfiguration configuration)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "Invalid token claims." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });

            user.FullName = dto.FullName?.Trim() ?? user.FullName;
            user.Email = dto.Email?.Trim().ToLowerInvariant() ?? user.Email;

            if (profilePicture != null && profilePicture.Length > 0)
            {
                var awsAccessKey = configuration["AWS:AccessKey"];
                var awsSecretKey = configuration["AWS:SecretKey"];
                var bucketName = configuration["AWS:BucketName"];
                var regionName = configuration["AWS:Region"];

                var region = Amazon.RegionEndpoint.GetBySystemName(regionName);
                var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, region);
                var fileTransferUtility = new TransferUtility(s3Client);

                var fileName = $"avatars/{Guid.NewGuid()}_{Path.GetFileName(profilePicture.FileName)}";

                using (var stream = profilePicture.OpenReadStream())
                {
                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = stream,
                        Key = fileName,
                        BucketName = bucketName
                    };

                    await fileTransferUtility.UploadAsync(uploadRequest);
                }

                // Save only the relative object key in the database (e.g., "avatars/guid_filename.jpg")
                user.ProfilePictureUrl = fileName;
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