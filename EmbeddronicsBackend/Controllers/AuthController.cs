using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Authenticate user with email and password
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous] // Allow anonymous access for login
        public async Task<ActionResult<ApiResponse<AuthResult>>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                
                if (result.Success)
                {
                    return Success(result, result.Message);
                }
                
                return BadRequest<AuthResult>(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return InternalServerError<AuthResult>("An internal error occurred");
            }
        }

        /// <summary>
        /// Verify OTP and complete authentication
        /// </summary>
        [HttpPost("verify-otp")]
        [AllowAnonymous] // Allow anonymous access for OTP verification
        public async Task<ActionResult<ApiResponse<AuthResult>>> VerifyOtp([FromBody] OtpVerificationRequest request)
        {
            try
            {
                var result = await _authService.VerifyOtpAsync(request);
                
                if (result.Success)
                {
                    return Success(result, result.Message);
                }
                
                return BadRequest<AuthResult>(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OTP verification for email: {Email}", request.Email);
                return InternalServerError<AuthResult>("An internal error occurred");
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous] // Allow anonymous access for token refresh
        public async Task<ActionResult<ApiResponse<AuthResult>>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);
                
                if (result.Success)
                {
                    return Success(result, result.Message);
                }
                
                return BadRequest<AuthResult>(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return InternalServerError<AuthResult>("An internal error occurred");
            }
        }

        /// <summary>
        /// Register a new client account
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous] // Allow anonymous access for registration
        public async Task<ActionResult<ApiResponse<bool>>> Register([FromBody] ClientRegistrationRequest request)
        {
            try
            {
                var result = await _authService.RegisterClientAsync(request);
                
                if (result)
                {
                    return Success(true, "Registration successful. Your account is pending approval.");
                }
                
                return BadRequest<bool>("Registration failed. Email may already be in use.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
                return InternalServerError<bool>("An internal error occurred");
            }
        }

        /// <summary>
        /// Logout and revoke refresh token
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> Logout([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.LogoutAsync(request.RefreshToken);
                
                if (result)
                {
                    return Success(true, "Logged out successfully");
                }
                
                return BadRequest<bool>("Logout failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return InternalServerError<bool>("An internal error occurred");
            }
        }

        /// <summary>
        /// Get current user information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync(User);
                
                if (user == null)
                {
                    return NotFound<UserDto>("User not found");
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = user.Name,
                    Role = user.Role,
                    Status = user.Status,
                    Company = user.Company,
                    Phone = user.Phone,
                    CreatedAt = user.CreatedAt
                };
                
                return Success(userDto, "User information retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user");
                return InternalServerError<UserDto>("An internal error occurred");
            }
        }

        /// <summary>
        /// Revoke all tokens for the current user (admin only)
        /// </summary>
        [HttpPost("revoke-all-tokens/{userId}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<ApiResponse<bool>>> RevokeAllUserTokens(int userId)
        {
            try
            {
                var result = await _authService.RevokeAllUserTokensAsync(userId);
                
                if (result)
                {
                    return Success(true, "All tokens revoked successfully");
                }
                
                return BadRequest<bool>("Failed to revoke tokens");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking tokens for user: {UserId}", userId);
                return InternalServerError<bool>("An internal error occurred");
            }
        }
    }
}
