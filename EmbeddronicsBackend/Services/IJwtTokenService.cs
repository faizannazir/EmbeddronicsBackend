using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using System.Security.Claims;

namespace EmbeddronicsBackend.Services
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        CustomTokenValidationResult ValidateToken(string token);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        Task<bool> IsRefreshTokenValidAsync(string refreshToken, int userId);
        Task SaveRefreshTokenAsync(int userId, string refreshToken);
        Task RevokeRefreshTokenAsync(string refreshToken);
        Task RevokeAllUserTokensAsync(int userId);
    }
}