using System;
using System.Collections.Generic;
using System.Text;

namespace Firefly.Application.Auth.Dtos
{
    public record RegisterRequest(
        string Username,
        string Email,
        string Password,
        string FullName,
        string Role = "User"
    );
}