using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EmbeddronicsBackend.Authorization.Requirements;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Authorization.Handlers
{
    public class ResourceOwnershipHandler : AuthorizationHandler<ResourceOwnershipRequirement, object>
    {
        private readonly ILogger<ResourceOwnershipHandler> _logger;
        private readonly EmbeddronicsDbContext _context;

        public ResourceOwnershipHandler(
            ILogger<ResourceOwnershipHandler> logger,
            EmbeddronicsDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ResourceOwnershipRequirement requirement,
            object resource)
        {
            var user = context.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Resource access denied - no user ID found in claims for {ResourceType}", 
                    requirement.ResourceType);
                context.Fail();
                return;
            }

            if (!int.TryParse(userId, out int userIdInt))
            {
                _logger.LogWarning("Resource access denied - invalid user ID format: {UserId} for {ResourceType}", 
                    userId, requirement.ResourceType);
                context.Fail();
                return;
            }

            // Admins can access all resources
            if (userRole == "admin")
            {
                _logger.LogInformation("Admin user {UserId} granted access to {ResourceType}", 
                    userId, requirement.ResourceType);
                context.Succeed(requirement);
                return;
            }

            // Check resource ownership based on resource type
            bool isOwner = await CheckResourceOwnership(requirement.ResourceType, resource, userIdInt);

            if (isOwner)
            {
                _logger.LogInformation("User {UserId} granted access to owned {ResourceType}", 
                    userId, requirement.ResourceType);
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} denied access to {ResourceType} - not owner or resource not found", 
                    userId, requirement.ResourceType);
                context.Fail();
            }
        }

        private async Task<bool> CheckResourceOwnership(string resourceType, object resource, int userId)
        {
            return resourceType switch
            {
                "Order" => await CheckOrderOwnership(resource, userId),
                "Quote" => await CheckQuoteOwnership(resource, userId),
                "Message" => await CheckMessageOwnership(resource, userId),
                "Invoice" => await CheckInvoiceOwnership(resource, userId),
                "Document" => await CheckDocumentOwnership(resource, userId),
                _ => false
            };
        }

        private async Task<bool> CheckOrderOwnership(object resource, int userId)
        {
            return resource switch
            {
                Order order => order.ClientId == userId,
                int orderId => await _context.Orders.AnyAsync(o => o.Id == orderId && o.ClientId == userId),
                string orderIdStr when int.TryParse(orderIdStr, out int id) => 
                    await _context.Orders.AnyAsync(o => o.Id == id && o.ClientId == userId),
                _ => false
            };
        }

        private async Task<bool> CheckQuoteOwnership(object resource, int userId)
        {
            return resource switch
            {
                Quote quote => quote.ClientId == userId,
                int quoteId => await _context.Quotes.AnyAsync(q => q.Id == quoteId && q.ClientId == userId),
                string quoteIdStr when int.TryParse(quoteIdStr, out int id) => 
                    await _context.Quotes.AnyAsync(q => q.Id == id && q.ClientId == userId),
                _ => false
            };
        }

        private async Task<bool> CheckMessageOwnership(object resource, int userId)
        {
            return resource switch
            {
                Message message => message.SenderId == userId || await IsMessageInUserOrder(message.OrderId, userId),
                int messageId => await IsMessageOwnedByUser(messageId, userId),
                string messageIdStr when int.TryParse(messageIdStr, out int id) => 
                    await IsMessageOwnedByUser(id, userId),
                _ => false
            };
        }

        private async Task<bool> CheckInvoiceOwnership(object resource, int userId)
        {
            return resource switch
            {
                Invoice invoice => await IsInvoiceOwnedByUser(invoice.OrderId, userId),
                int invoiceId => await _context.Invoices
                    .Include(i => i.Order)
                    .AnyAsync(i => i.Id == invoiceId && i.Order.ClientId == userId),
                string invoiceIdStr when int.TryParse(invoiceIdStr, out int id) => 
                    await _context.Invoices
                        .Include(i => i.Order)
                        .AnyAsync(i => i.Id == id && i.Order.ClientId == userId),
                _ => false
            };
        }

        private async Task<bool> CheckDocumentOwnership(object resource, int userId)
        {
            return resource switch
            {
                Document document => await IsDocumentOwnedByUser(document.OrderId, userId),
                int documentId => await _context.Documents
                    .Include(d => d.Order)
                    .AnyAsync(d => d.Id == documentId && d.Order.ClientId == userId),
                string documentIdStr when int.TryParse(documentIdStr, out int id) => 
                    await _context.Documents
                        .Include(d => d.Order)
                        .AnyAsync(d => d.Id == id && d.Order.ClientId == userId),
                _ => false
            };
        }

        private async Task<bool> IsMessageInUserOrder(int orderId, int userId)
        {
            return await _context.Orders.AnyAsync(o => o.Id == orderId && o.ClientId == userId);
        }

        private async Task<bool> IsMessageOwnedByUser(int messageId, int userId)
        {
            return await _context.Messages
                .Include(m => m.Order)
                .AnyAsync(m => m.Id == messageId && 
                    (m.SenderId == userId || m.Order.ClientId == userId));
        }

        private async Task<bool> IsInvoiceOwnedByUser(int orderId, int userId)
        {
            return await _context.Orders.AnyAsync(o => o.Id == orderId && o.ClientId == userId);
        }

        private async Task<bool> IsDocumentOwnedByUser(int orderId, int userId)
        {
            return await _context.Orders.AnyAsync(o => o.Id == orderId && o.ClientId == userId);
        }
    }
}