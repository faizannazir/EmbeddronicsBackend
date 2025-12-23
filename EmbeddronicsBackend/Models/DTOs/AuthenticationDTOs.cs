using System.ComponentModel.DataAnnotations;

namespace EmbeddronicsBackend.Models.DTOs
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "client";
    }

    public class OtpVerificationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Otp { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public UserDto? User { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool RequiresOtp { get; set; }
        public DateTime? TokenExpiration { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomTokenValidationResult
    {
        public bool IsValid { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? ErrorMessage { get; set; }
    }
}