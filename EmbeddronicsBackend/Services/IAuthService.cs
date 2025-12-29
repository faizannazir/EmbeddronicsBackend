using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using System.Security.Claims;

namespace EmbeddronicsBackend.Services
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(LoginRequest request);
        Task<AuthResult> VerifyOtpAsync(OtpVerificationRequest request);
        Task<AuthResult> RefreshTokenAsync(string refreshToken);
        Task<bool> RegisterClientAsync(ClientRegistrationRequest request);
        Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal);
        Task<bool> LogoutAsync(string refreshToken);
        Task<bool> LogoutAsync(string refreshToken, string? accessToken = null);
        Task<bool> RevokeAllUserTokensAsync(int userId);
    }
}