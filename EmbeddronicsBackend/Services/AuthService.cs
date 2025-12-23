using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Models.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EmbeddronicsBackend.Services
{
    public class AuthService : IAuthService
    {
        private readonly EmbeddronicsDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<AuthService> _logger;
        
        // In-memory OTP store for demo purposes
        // In production, use Redis or database with expiration
        private static readonly Dictionary<string, (string Otp, DateTime Expiry)> _otpStore = new();

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

                // Generate and store OTP
                var otp = GenerateOtp();
                _otpStore[request.Email] = (otp, DateTime.UtcNow.AddMinutes(5)); // 5-minute expiry

                _logger.LogInformation("OTP generated for user: {Email}, OTP: {OTP}", request.Email, otp);
                
                // In production, send OTP via email/SMS service
                return new AuthResult
                {
                    Success = true,
                    RequiresOtp = true,
                    Message = "OTP sent to your registered email address"
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
            try
            {
                _logger.LogInformation("OTP verification attempt for email: {Email}", request.Email);

                if (!_otpStore.TryGetValue(request.Email, out var otpData))
                {
                    _logger.LogWarning("OTP verification failed - no OTP found: {Email}", request.Email);
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Invalid or expired OTP"
                    };
                }

                if (DateTime.UtcNow > otpData.Expiry)
                {
                    _otpStore.Remove(request.Email);
                    _logger.LogWarning("OTP verification failed - expired: {Email}", request.Email);
                    return new AuthResult
                    {
                        Success = false,
                        Message = "OTP has expired"
                    };
                }

                if (otpData.Otp != request.Otp)
                {
                    _logger.LogWarning("OTP verification failed - invalid OTP: {Email}", request.Email);
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Invalid OTP"
                    };
                }

                // Remove OTP after successful verification
                _otpStore.Remove(request.Email);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Generate tokens
                var accessToken = _jwtTokenService.GenerateAccessToken(user);
                var refreshToken = _jwtTokenService.GenerateRefreshToken();

                // Save refresh token
                await _jwtTokenService.SaveRefreshTokenAsync(user.Id, refreshToken);

                _logger.LogInformation("OTP verified and tokens issued for user: {Email}", request.Email);

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
                _logger.LogError(ex, "Error during OTP verification for email: {Email}", request.Email);
                return new AuthResult
                {
                    Success = false,
                    Message = "An error occurred during OTP verification"
                };
            }
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

        private string GenerateOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            return (BitConverter.ToUInt32(bytes, 0) % 1000000).ToString("D6");
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "EmbeddronicsSalt2024"));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            var computedHash = HashPassword(password);
            return computedHash == hash;
        }
    }
}