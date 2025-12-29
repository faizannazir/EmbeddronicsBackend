using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Configuration;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EmbeddronicsBackend.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly EmbeddronicsDbContext _context;
        private readonly ITokenBlacklistService _blacklistService;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(
            IOptions<JwtSettings> jwtSettings,
            EmbeddronicsDbContext context,
            ITokenBlacklistService blacklistService,
            ILogger<JwtTokenService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _context = context;
            _blacklistService = blacklistService;
            _logger = logger;
        }

        public string GenerateAccessToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("status", user.Status),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), 
                    ClaimValueTypes.Integer64)
            };

            // Add role-specific scope claims for authorization policies
            if (user.Role == "admin")
            {
                claims.Add(new Claim("scope", "crm"));
                claims.Add(new Claim("scope", "admin"));
                claims.Add(new Claim("scope", "manage_all"));
            }
            else if (user.Role == "client")
            {
                claims.Add(new Claim("scope", "portal"));
                claims.Add(new Claim("scope", "client"));
                claims.Add(new Claim("scope", "view_own"));
            }

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            
            _logger.LogInformation("Access token generated for user {UserId} ({Email}) with role {Role}", 
                user.Id, user.Email, user.Role);
            
            return tokenString;
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public CustomTokenValidationResult ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is JwtSecurityToken jwtToken &&
                    jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    // Check if token is blacklisted
                    var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti) && _blacklistService.IsTokenBlacklistedAsync(jti).Result)
                    {
                        return new CustomTokenValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "Token has been revoked"
                        };
                    }

                    return new CustomTokenValidationResult
                    {
                        IsValid = true,
                        UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                        Email = principal.FindFirst(ClaimTypes.Email)?.Value,
                        Role = principal.FindFirst(ClaimTypes.Role)?.Value
                    };
                }

                return new CustomTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid token algorithm"
                };
            }
            catch (SecurityTokenExpiredException)
            {
                return new CustomTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token has expired"
                };
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning("Token validation failed: {Error}", ex.Message);
                return new CustomTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid token"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation");
                return new CustomTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token validation error"
                };
            }
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = false // Don't validate lifetime for expired tokens
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get principal from expired token");
                return null;
            }
        }

        public async Task<bool> IsRefreshTokenValidAsync(string refreshToken, int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.RefreshToken == refreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }

        public async Task SaveRefreshTokenAsync(int userId, string refreshToken)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Refresh token saved for user {UserId}", userId);
            }
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Refresh token revoked for user {UserId}", user.Id);
            }
        }

        public async Task RevokeAllUserTokensAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("All tokens revoked for user {UserId}", userId);
            }
        }

        public async Task BlacklistTokenAsync(string token)
        {
            var jti = ExtractJtiFromToken(token);
            if (!string.IsNullOrEmpty(jti))
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var expiration = jwtToken.ValidTo;
                
                await _blacklistService.BlacklistTokenAsync(jti, expiration);
                _logger.LogInformation("Token blacklisted with JTI: {Jti}", jti);
            }
        }

        public async Task<bool> IsTokenBlacklistedAsync(string token)
        {
            var jti = ExtractJtiFromToken(token);
            if (string.IsNullOrEmpty(jti))
            {
                return false;
            }

            return await _blacklistService.IsTokenBlacklistedAsync(jti);
        }

        public string? ExtractJtiFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                return jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract JTI from token");
                return null;
            }
        }
    }
}