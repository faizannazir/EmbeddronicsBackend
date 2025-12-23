using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using EmbeddronicsBackend.Models.Exceptions;
using Serilog;

namespace EmbeddronicsBackend.Attributes
{
    /// <summary>
    /// Authorization attribute that requires specific roles and throws custom exceptions
    /// </summary>
    public class RequireRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public RequireRoleAttribute(params string[] roles)
        {
            _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // Check if user is authenticated
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                Log.Warning("Unauthorized access attempt to {Action} by unauthenticated user", 
                    context.ActionDescriptor.DisplayName);
                throw new UnauthorizedOperationException("Authentication required");
            }

            // Check if user has required role
            if (_roles.Length > 0 && !_roles.Any(role => user.IsInRole(role)))
            {
                var userRoles = user.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                Log.Warning("Access denied to {Action} for user {User} with roles [{UserRoles}]. Required roles: [{RequiredRoles}]", 
                    context.ActionDescriptor.DisplayName,
                    user.Identity?.Name ?? "unknown",
                    string.Join(", ", userRoles),
                    string.Join(", ", _roles));

                throw new UnauthorizedOperationException($"Access denied. Required role(s): {string.Join(", ", _roles)}");
            }

            Log.Information("Authorized access to {Action} by user {User}", 
                context.ActionDescriptor.DisplayName, 
                user.Identity?.Name ?? "unknown");
        }
    }

    /// <summary>
    /// Convenience attribute for admin-only operations
    /// </summary>
    public class AdminOnlyAttribute : RequireRoleAttribute
    {
        public AdminOnlyAttribute() : base("admin")
        {
        }
    }

    /// <summary>
    /// Convenience attribute for client-only operations
    /// </summary>
    public class ClientOnlyAttribute : RequireRoleAttribute
    {
        public ClientOnlyAttribute() : base("client")
        {
        }
    }

    /// <summary>
    /// Convenience attribute for operations that allow both admin and client roles
    /// </summary>
    public class AuthenticatedUserAttribute : RequireRoleAttribute
    {
        public AuthenticatedUserAttribute() : base("admin", "client")
        {
        }
    }
}