using SolidShortener.Application.Users.DTOs;

namespace SolidShortener.Application.Interfaces.Services;

public interface ITokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(UserDTO user);
}
