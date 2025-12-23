using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;
using System.Text.Json;

namespace EmbeddronicsBackend.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IJwtTokenService jwtTokenService)
        {
            try
            {
                var token = ExtractTokenFromHeader(context);
                
                if (!string.IsNullOrEmpty(token))
                {
                    var validationResult = jwtTokenService.ValidateToken(token);
                    
                    if (!validationResult.IsValid)
                    {
                        _logger.LogWarning("Invalid token detected: {Error}", validationResult.ErrorMessage);
                        
                        // Don't block the request, let the authorization attributes handle it
                        // This middleware is for logging and monitoring purposes
                    }
                    else
                    {
                        // Token is valid, add user info to context for logging
                        context.Items["UserId"] = validationResult.UserId;
                        context.Items["UserEmail"] = validationResult.Email;
                        context.Items["UserRole"] = validationResult.Role;
                    }
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in authentication middleware");
                
                // Don't block the request on middleware errors
                await _next(context);
            }
        }

        private string? ExtractTokenFromHeader(HttpContext context)
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            
            if (string.IsNullOrEmpty(authHeader))
                return null;

            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            return null;
        }
    }

    public static class AuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>();
        }
    }
}