namespace Firefly.Application.Users.Dtos
{
    public record UserResponseDto(
        string Id,
        string Username,
        string Email,
        string FullName,
        string ProfilePictureUrl,
        bool IsActive,
        IEnumerable<string> Roles,
        DateTime CreatedAt
    );

    public record CreateUserDto(
        string Username,
        string Email,
        string Password,
        string FullName,
        string ProfilePictureUrl,
        string Role
    );

    public record UpdateUserDto(
        string FullName,
        string Email,
        string? ProfilePictureUrl,
        string Role,
        bool IsActive
    );

    public record ResetPasswordDto(
        string NewPassword
    );
}