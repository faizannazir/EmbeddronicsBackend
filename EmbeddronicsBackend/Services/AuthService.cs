using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Models.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using BCrypt.Net;

namespace EmbeddronicsBackend.Services
{
    public class AuthService : IAuthService
    {
        private readonly EmbeddronicsDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            EmbeddronicsDbContext context,
            IJwtTokenService jwtTokenService,
            ILogger<AuthService> logger)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task<AuthResult> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                {
                    _logger.LogWarning("Login failed - user not found: {Email}", request.Email);
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                if (user.Status != "active")
                {
                    _logger.LogWarning("Login failed - user not active: {Email}, Status: {Status}", 
                        request.Email, user.Status);
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Account is not active. Please contact administrator."
                    };
                }

                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed - invalid password: {Email}", request.Email);
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                // Check if user is using legacy password and migrate to BCrypt
                await MigrateUserPasswordIfNeeded(user, request.Password);

                // Generate tokens directly (no OTP required)
                var accessToken = _jwtTokenService.GenerateAccessToken(user);
                var refreshToken = _jwtTokenService.GenerateRefreshToken();

                // Save refresh token
                await _jwtTokenService.SaveRefreshTokenAsync(user.Id, refreshToken);

                _logger.LogInformation("Login successful for user: {Email}", request.Email);

                return new AuthResult
                {
                    Success = true,
                    Token = accessToken,
                    RefreshToken = refreshToken,
                    TokenExpiration = DateTime.UtcNow.AddHours(1), // Match JWT expiration
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        Role = user.Role,
                        Status = user.Status,
                        Company = user.Company,
                        Phone = user.Phone,
                        CreatedAt = user.CreatedAt
                    },
                    Message = "Login successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return new AuthResult
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        public async Task<AuthResult> VerifyOtpAsync(OtpVerificationRequest request)
        {
            // OTP functionality has been removed - return error
            _logger.LogWarning("OTP verification called but OTP functionality has been removed");
            return new AuthResult
            {
                Success = false,
                Message = "OTP verification is no longer supported. Please use regular login."
            };
        }

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Refresh token attempt");

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

                if (user == null || !await _jwtTokenService.IsRefreshTokenValidAsync(refreshToken, user.Id))
                {
                    _logger.LogWarning("Refresh token validation failed");
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Invalid or expired refresh token"
                    };
                }

                // Generate new tokens
                var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
                var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

                // Save new refresh token
                await _jwtTokenService.SaveRefreshTokenAsync(user.Id, newRefreshToken);

                _logger.LogInformation("Tokens refreshed for user: {UserId}", user.Id);

                return new AuthResult
                {
                    Success = true,
                    Token = newAccessToken,
                    RefreshToken = newRefreshToken,
                    TokenExpiration = DateTime.UtcNow.AddHours(1),
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        Role = user.Role,
                        Status = user.Status,
                        Company = user.Company,
                        Phone = user.Phone,
                        CreatedAt = user.CreatedAt
                    },
                    Message = "Token refreshed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return new AuthResult
                {
                    Success = false,
                    Message = "An error occurred during token refresh"
                };
            }
        }

        public async Task<bool> RegisterClientAsync(ClientRegistrationRequest request)
        {
            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return false;
                }

                var user = new User
                {
                    Email = request.Email,
                    Name = request.Name,
                    PasswordHash = HashPassword(request.Password),
                    Role = "client",
                    Status = "pending", // Requires admin approval
                    Company = request.Company,
                    Phone = request.Phone,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New client registered: {Email}", request.Email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during client registration for email: {Email}", request.Email);
                return false;
            }
        }

        public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            return await _context.Users.FindAsync(userId);
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            try
            {
                await _jwtTokenService.RevokeRefreshTokenAsync(refreshToken);
                _logger.LogInformation("User logged out successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<bool> LogoutAsync(string refreshToken, string? accessToken = null)
        {
            try
            {
                // Revoke refresh token
                await _jwtTokenService.RevokeRefreshTokenAsync(refreshToken);
                
                // Blacklist access token if provided
                if (!string.IsNullOrEmpty(accessToken))
                {
                    await _jwtTokenService.BlacklistTokenAsync(accessToken);
                }
                
                _logger.LogInformation("User logged out successfully with token blacklisting");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<bool> RevokeAllUserTokensAsync(int userId)
        {
            try
            {
                await _jwtTokenService.RevokeAllUserTokensAsync(userId);
                _logger.LogInformation("All tokens revoked for user: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking tokens for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ChangeOwnPasswordAsync(int userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Change own password failed - user not found: {UserId}", userId);
                    return false;
                }

                // Verify current password
                if (!VerifyPassword(currentPassword, user.PasswordHash))
                {
                    _logger.LogWarning("Change own password failed - invalid current password for user: {UserId}", userId);
                    return false;
                }

                // Update to new password (hash it)
                user.PasswordHash = HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Revoke existing tokens so user must re-authenticate
                await RevokeAllUserTokensAsync(userId);

                _logger.LogInformation("Password changed for user: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                return false;
            }
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        private bool VerifyPassword(string password, string hash)
        {
            try
            {
                // First try BCrypt verification (new method)
                if (BCrypt.Net.BCrypt.Verify(password, hash))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // If BCrypt fails, it might be a legacy hash, continue to legacy verification
            }

            // Fallback to legacy SHA256 verification for existing users
            try
            {
                var legacyHash1 = LegacyHashPassword(password, "EmbeddronicsSalt2024");
                var legacyHash2 = LegacyHashPassword(password, "EmbeddronicsSalt");
                
                if (hash == legacyHash1 || hash == legacyHash2)
                {
                    _logger.LogInformation("Legacy password verification successful - password should be migrated");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Legacy password verification failed");
            }

            return false;
        }

        private string LegacyHashPassword(string password, string salt)
        {
            // Legacy SHA256 hashing method for backward compatibility
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + salt));
            return Convert.ToBase64String(hashedBytes);
        }

        private async Task MigrateUserPasswordIfNeeded(User user, string plainTextPassword)
        {
            try
            {
                // Check if current password is a legacy hash
                var legacyHash1 = LegacyHashPassword(plainTextPassword, "EmbeddronicsSalt2024");
                var legacyHash2 = LegacyHashPassword(plainTextPassword, "EmbeddronicsSalt");

                if (user.PasswordHash == legacyHash1 || user.PasswordHash == legacyHash2)
                {
                    // Migrate to BCrypt
                    user.PasswordHash = HashPassword(plainTextPassword);
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Migrated password to BCrypt for user: {Email}", user.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate password for user: {Email}", user.Email);
                // Don't fail the login if migration fails
            }
        }
    }
}