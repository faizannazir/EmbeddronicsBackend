using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EmbeddronicsBackend.Authorization.Requirements;

namespace EmbeddronicsBackend.Authorization.Handlers
{
    /// <summary>
    /// Authorization handler for admin-only operations
    /// </summary>
    public class AdminOperationHandler : AuthorizationHandler<AdminOperationRequirement>
    {
        private readonly ILogger<AdminOperationHandler> _logger;

        public AdminOperationHandler(ILogger<AdminOperationHandler> logger)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminOperationRequirement requirement)
        {
            var user = context.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
            var userName = user.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Admin operation {Operation} on {ResourceType} denied - no user ID found in claims", 
                    requirement.Operation, requirement.ResourceType ?? "unknown");
                context.Fail();
                return Task.CompletedTask;
            }

            if (userRole != "admin")
            {
                _logger.LogWarning("Admin operation {Operation} on {ResourceType} denied for user {UserId} ({UserName}) - insufficient privileges. User role: {UserRole}", 
                    requirement.Operation, requirement.ResourceType ?? "unknown", userId, userName ?? "unknown", userRole ?? "none");
                context.Fail();
                return Task.CompletedTask;
            }

            // Check if user has admin scope claim
            var hasAdminScope = user.HasClaim("scope", "admin") || user.HasClaim("scope", "crm");
            if (!hasAdminScope)
            {
                _logger.LogWarning("Admin operation {Operation} on {ResourceType} denied for user {UserId} ({UserName}) - missing admin scope", 
                    requirement.Operation, requirement.ResourceType ?? "unknown", userId, userName ?? "unknown");
                context.Fail();
                return Task.CompletedTask;
            }

            _logger.LogInformation("Admin operation {Operation} on {ResourceType} authorized for user {UserId} ({UserName})", 
                requirement.Operation, requirement.ResourceType ?? "unknown", userId, userName ?? "unknown");
            
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}