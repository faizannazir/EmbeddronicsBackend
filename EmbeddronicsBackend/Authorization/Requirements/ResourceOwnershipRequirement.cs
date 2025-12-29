using Microsoft.AspNetCore.Authorization;

namespace EmbeddronicsBackend.Authorization.Requirements
{
    public class ResourceOwnershipRequirement : IAuthorizationRequirement
    {
        public string ResourceType { get; }

        public ResourceOwnershipRequirement(string resourceType)
        {
            ResourceType = resourceType;
        }
    }
}