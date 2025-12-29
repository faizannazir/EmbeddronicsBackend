using Microsoft.AspNetCore.Authorization;

namespace EmbeddronicsBackend.Authorization.Attributes
{
    /// <summary>
    /// Base class for policy-based authorization attributes
    /// </summary>
    public class PolicyAuthorizationAttribute : AuthorizeAttribute
    {
        public PolicyAuthorizationAttribute(string policy) : base(policy)
        {
        }
    }

    /// <summary>
    /// Requires admin role for CRM operations
    /// </summary>
    public class AdminCRMAttribute : PolicyAuthorizationAttribute
    {
        public AdminCRMAttribute() : base("AdminCRM")
        {
        }
    }

    /// <summary>
    /// Requires client role for portal operations
    /// </summary>
    public class ClientPortalAttribute : PolicyAuthorizationAttribute
    {
        public ClientPortalAttribute() : base("ClientPortal")
        {
        }
    }

    /// <summary>
    /// Requires admin role for product management
    /// </summary>
    public class ManageProductsAttribute : PolicyAuthorizationAttribute
    {
        public ManageProductsAttribute() : base("ManageProducts")
        {
        }
    }

    /// <summary>
    /// Requires admin role for order management
    /// </summary>
    public class ManageOrdersAttribute : PolicyAuthorizationAttribute
    {
        public ManageOrdersAttribute() : base("ManageOrders")
        {
        }
    }

    /// <summary>
    /// Requires admin role for quote management
    /// </summary>
    public class ManageQuotesAttribute : PolicyAuthorizationAttribute
    {
        public ManageQuotesAttribute() : base("ManageQuotes")
        {
        }
    }

    /// <summary>
    /// Requires admin role for client management
    /// </summary>
    public class ManageClientsAttribute : PolicyAuthorizationAttribute
    {
        public ManageClientsAttribute() : base("ManageClients")
        {
        }
    }

    /// <summary>
    /// Allows public access (no authentication required)
    /// </summary>
    public class PublicAccessAttribute : PolicyAuthorizationAttribute
    {
        public PublicAccessAttribute() : base("PublicAccess")
        {
        }
    }

    /// <summary>
    /// Requires admin or client role
    /// </summary>
    public class AuthenticatedUserAttribute : PolicyAuthorizationAttribute
    {
        public AuthenticatedUserAttribute() : base("AdminOrClient")
        {
        }
    }
}