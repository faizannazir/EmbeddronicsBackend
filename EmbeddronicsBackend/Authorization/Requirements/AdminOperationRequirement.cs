using Microsoft.AspNetCore.Authorization;

namespace EmbeddronicsBackend.Authorization.Requirements
{
    /// <summary>
    /// Requirement for admin-only operations with specific operation types
    /// </summary>
    public class AdminOperationRequirement : IAuthorizationRequirement
    {
        public string Operation { get; }
        public string? ResourceType { get; }

        public AdminOperationRequirement(string operation, string? resourceType = null)
        {
            Operation = operation;
            ResourceType = resourceType;
        }
    }

    /// <summary>
    /// Common admin operations
    /// </summary>
    public static class AdminOperations
    {
        public const string Create = "create";
        public const string Read = "read";
        public const string Update = "update";
        public const string Delete = "delete";
        public const string Manage = "manage";
        public const string Approve = "approve";
        public const string Reject = "reject";
        public const string Export = "export";
        public const string Import = "import";
    }

    /// <summary>
    /// Resource types for admin operations
    /// </summary>
    public static class ResourceTypes
    {
        public const string Client = "client";
        public const string Order = "order";
        public const string Quote = "quote";
        public const string Product = "product";
        public const string Service = "service";
        public const string Project = "project";
        public const string Blog = "blog";
        public const string User = "user";
        public const string Financial = "financial";
        public const string Lead = "lead";
        public const string Review = "review";
    }
}