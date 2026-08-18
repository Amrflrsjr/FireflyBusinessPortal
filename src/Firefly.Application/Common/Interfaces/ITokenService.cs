namespace Firefly.Application.Common.Interfaces
{
    public interface ITokenService
    {
        string GenerateJwtToken(string userId, string username, string email, IEnumerable<string> roles);
    }
}