using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using EmbeddronicsBackend.Authorization.Requirements;
using EmbeddronicsBackend.Models.Exceptions;

namespace EmbeddronicsBackend.Authorization.Attributes
{
    public class ResourceOwnershipAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _resourceType;
        private readonly string _resourceParameterName;

        public ResourceOwnershipAttribute(string resourceType, string resourceParameterName = "id")
        {
            _resourceType = resourceType;
            _resourceParameterName = resourceParameterName;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authorizationService = context.HttpContext.RequestServices
                .GetRequiredService<IAuthorizationService>();

            // Get the resource ID from route parameters
            var resourceId = context.RouteData.Values[_resourceParameterName];
            
            var requirement = new ResourceOwnershipRequirement(_resourceType);
            var authResult = await authorizationService.AuthorizeAsync(
                context.HttpContext.User, 
                resourceId, 
                requirement);

            if (!authResult.Succeeded)
            {
                throw new UnauthorizedOperationException($"Access denied to {_resourceType.ToLower()}");
            }
        }
    }

    // Convenience attributes for specific resources
    public class OrderOwnershipAttribute : ResourceOwnershipAttribute
    {
        public OrderOwnershipAttribute(string parameterName = "id") : base("Order", parameterName) { }
    }

    public class QuoteOwnershipAttribute : ResourceOwnershipAttribute
    {
        public QuoteOwnershipAttribute(string parameterName = "id") : base("Quote", parameterName) { }
    }

    public class MessageOwnershipAttribute : ResourceOwnershipAttribute
    {
        public MessageOwnershipAttribute(string parameterName = "id") : base("Message", parameterName) { }
    }
}