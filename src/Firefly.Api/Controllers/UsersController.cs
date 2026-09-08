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
        private readonly IConfiguration _configuration;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        private AmazonS3Client CreateS3Client()
        {
            var regionName = _configuration["AWS:Region"];
            var region = !string.IsNullOrEmpty(regionName)
                ? Amazon.RegionEndpoint.GetBySystemName(regionName)
                : Amazon.RegionEndpoint.APSoutheast1;

            var awsAccessKey = _configuration["AWS:AccessKey"];
            var awsSecretKey = _configuration["AWS:SecretKey"];

            if (!string.IsNullOrEmpty(awsAccessKey) && !string.IsNullOrEmpty(awsSecretKey))
            {
                return new AmazonS3Client(awsAccessKey, awsSecretKey, region);
            }

            return new AmazonS3Client(region);
        }

        private string GetPreSignedProfilePictureUrl(string? profilePictureUrl)
        {
            if (string.IsNullOrEmpty(profilePictureUrl)) return string.Empty;

            try
            {
                var bucketName = _configuration["AWS:BucketName"];
                var s3Client = CreateS3Client();

                // Normalize the object key (strip out any full domain prefix if previously stored)
                var objectKey = profilePictureUrl.StartsWith("http")
                    ? new Uri(profilePictureUrl).AbsolutePath.TrimStart('/')
                    : profilePictureUrl.TrimStart('/');

                var request = new Amazon.S3.Model.GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    Expires = DateTime.UtcNow.AddHours(2)
                };

                return s3Client.GetPreSignedURL(request);
            }
            catch
            {
                // Fallback to stored string if pre-signing fails
                return profilePictureUrl;
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "Invalid token claims." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });

            var pictureUrl = GetPreSignedProfilePictureUrl(user.ProfilePictureUrl);

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
            IFormFile? profilePicture)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "Invalid token claims." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found" });

            user.FullName = dto.FullName?.Trim() ?? user.FullName;
            user.Email = dto.Email?.Trim().ToLowerInvariant() ?? user.Email;

            if (profilePicture != null && profilePicture.Length > 0)
            {
                var bucketName = _configuration["AWS:BucketName"];
                var s3Client = CreateS3Client();
                var fileTransferUtility = new TransferUtility(s3Client);

                // Sanitize the file name to strip out spaces and encoded characters (e.g., %20)
                var cleanFileName = Path.GetFileName(profilePicture.FileName)
                    .Replace(" ", "_")
                    .Replace("%20", "_");

                var fileName = $"avatars/{Guid.NewGuid()}_{cleanFileName}";

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

                // Save only the clean relative object key in the database
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

            var updatedPictureUrl = GetPreSignedProfilePictureUrl(user.ProfilePictureUrl);

            return Ok(new { message = "Profile updated successfully", profilePictureUrl = updatedPictureUrl });
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
                var pictureUrl = GetPreSignedProfilePictureUrl(user.ProfilePictureUrl);

                userList.Add(new UserResponseDto(
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

            return Ok(userList);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            var pictureUrl = GetPreSignedProfilePictureUrl(user.ProfilePictureUrl);

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