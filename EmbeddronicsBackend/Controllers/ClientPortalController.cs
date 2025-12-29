using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Authorization.Attributes;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EmbeddronicsBackend.Models.Exceptions;
using Serilog;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/client")]
    [Authorize] // Require authentication for all client portal actions
    public class ClientPortalController : BaseApiController
    {
        private readonly EmbeddronicsDbContext _context;

        public ClientPortalController(EmbeddronicsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get current client's profile information
        /// </summary>
        [HttpGet("profile")]
        [ClientPortal] // Client portal access required
        public async Task<ActionResult<ApiResponse<object>>> GetProfile()
        {
            var userId = GetCurrentUserId();
            
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Name,
                    u.Company,
                    u.Phone,
                    u.Status,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new NotFoundException("User", userId);
            }

            Log.Information("Profile accessed by client {UserId}", userId);
            return Success((object)user, "Profile retrieved successfully");
        }

        /// <summary>
        /// Update current client's profile information
        /// </summary>
        [HttpPut("profile")]
        [ClientPortal] // Client portal access required
        public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetCurrentUserId();
            
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User", userId);
            }

            // Update allowed fields
            if (!string.IsNullOrWhiteSpace(request.Name))
                user.Name = request.Name;
            
            if (!string.IsNullOrWhiteSpace(request.Company))
                user.Company = request.Company;
            
            if (!string.IsNullOrWhiteSpace(request.Phone))
                user.Phone = request.Phone;

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Log.Information("Profile updated by client {UserId}", userId);
            return Success((object)new { message = "Profile updated successfully" }, "Profile updated successfully");
        }

        /// <summary>
        /// Get client's orders with resource ownership validation
        /// </summary>
        [HttpGet("orders")]
        [ClientPortal] // Client portal access required
        public async Task<ActionResult<ApiResponse<object>>> GetOrders()
        {
            var userId = GetCurrentUserId();
            
            var orders = await _context.Orders
                .Where(o => o.ClientId == userId)
                .Select(o => new
                {
                    o.Id,
                    o.Title,
                    o.Status,
                    o.Description,
                    o.BudgetRange,
                    o.Timeline,
                    o.CreatedAt,
                    o.UpdatedAt,
                    MessageCount = o.Messages.Count(),
                    DocumentCount = o.Documents.Count()
                })
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            Log.Information("Orders retrieved for client {UserId}, count: {OrderCount}", userId, orders.Count);
            return Success((object)orders, "Orders retrieved successfully");
        }

        /// <summary>
        /// Get specific order details with ownership validation
        /// </summary>
        [HttpGet("orders/{id}")]
        [OrderOwnership] // Resource ownership validation
        public async Task<ActionResult<ApiResponse<object>>> GetOrder(int id)
        {
            var userId = GetCurrentUserId();
            
            var order = await _context.Orders
                .Include(o => o.Messages.OrderBy(m => m.CreatedAt))
                .Include(o => o.Documents)
                .Where(o => o.Id == id && o.ClientId == userId)
                .Select(o => new
                {
                    o.Id,
                    o.Title,
                    o.Status,
                    o.Description,
                    o.PcbSpecs,
                    o.BudgetRange,
                    o.Timeline,
                    o.CreatedAt,
                    o.UpdatedAt,
                    Messages = o.Messages.Select(m => new
                    {
                        m.Id,
                        m.Content,
                        m.SenderId,
                        m.IsRead,
                        m.CreatedAt,
                        SenderName = m.Sender.Name
                    }),
                    Documents = o.Documents.Select(d => new
                    {
                        d.Id,
                        d.FileName,
                        d.FileType,
                        d.FileSize,
                        UploadedAt = d.CreatedAt
                    })
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                throw new NotFoundException("Order", id);
            }

            Log.Information("Order {OrderId} details accessed by client {UserId}", id, userId);
            return Success((object)order!, "Order details retrieved successfully");
        }

        /// <summary>
        /// Get client's quotes with resource ownership validation
        /// </summary>
        [HttpGet("quotes")]
        [ClientPortal] // Client portal access required
        public async Task<ActionResult<ApiResponse<object>>> GetQuotes()
        {
            var userId = GetCurrentUserId();
            
            var quotes = await _context.Quotes
                .Include(q => q.Order)
                .Where(q => q.ClientId == userId)
                .Select(q => new
                {
                    q.Id,
                    q.Amount,
                    q.Currency,
                    q.Status,
                    q.ValidUntil,
                    q.CreatedAt,
                    OrderTitle = q.Order != null ? q.Order.Title : "Unknown Order",
                    OrderId = q.OrderId
                })
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            Log.Information("Quotes retrieved for client {UserId}, count: {QuoteCount}", userId, quotes.Count);
            return Success((object)quotes, "Quotes retrieved successfully");
        }

        /// <summary>
        /// Get specific quote details with ownership validation
        /// </summary>
        [HttpGet("quotes/{id}")]
        [QuoteOwnership] // Resource ownership validation
        public async Task<ActionResult<ApiResponse<object>>> GetQuote(int id)
        {
            var userId = GetCurrentUserId();
            
            var quote = await _context.Quotes
                .Include(q => q.Order)
                .Include(q => q.Items)
                .Where(q => q.Id == id && q.ClientId == userId)
                .Select(q => new
                {
                    q.Id,
                    q.Amount,
                    q.Currency,
                    q.Status,
                    q.ValidUntil,
                    q.CreatedAt,
                    Order = q.Order != null ? new
                    {
                        q.Order.Id,
                        q.Order.Title,
                        q.Order.Description
                    } : null,
                    Items = q.Items.Select(i => new
                    {
                        i.Id,
                        i.Description,
                        i.Quantity,
                        i.UnitPrice,
                        i.TotalPrice
                    })
                })
                .FirstOrDefaultAsync();

            if (quote == null)
            {
                throw new NotFoundException("Quote", id);
            }

            Log.Information("Quote {QuoteId} details accessed by client {UserId}", id, userId);
            return Success((object)quote!, "Quote details retrieved successfully");
        }

        /// <summary>
        /// Accept a quote (client action)
        /// </summary>
        [HttpPost("quotes/{id}/accept")]
        [QuoteOwnership] // Resource ownership validation
        public async Task<ActionResult<ApiResponse<object>>> AcceptQuote(int id)
        {
            var userId = GetCurrentUserId();
            
            var quote = await _context.Quotes
                .Where(q => q.Id == id && q.ClientId == userId)
                .FirstOrDefaultAsync();

            if (quote == null)
            {
                throw new NotFoundException("Quote", id);
            }

            if (quote.Status != "pending")
            {
                throw new ValidationException("status", "Only pending quotes can be accepted");
            }

            if (quote.ValidUntil < DateTime.UtcNow)
            {
                throw new ValidationException("validUntil", "Quote has expired");
            }

            quote.Status = "accepted";
            quote.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Log.Information("Quote {QuoteId} accepted by client {UserId}", id, userId);
            return Success((object)new { message = "Quote accepted successfully" }, "Quote accepted successfully");
        }

        /// <summary>
        /// Get client's invoices
        /// </summary>
        [HttpGet("invoices")]
        [ClientPortal] // Client portal access required
        public async Task<ActionResult<ApiResponse<object>>> GetInvoices()
        {
            var userId = GetCurrentUserId();
            
            var invoices = await _context.Invoices
                .Include(i => i.Order)
                .Where(i => i.Order.ClientId == userId)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceNumber,
                    i.Amount,
                    i.Currency,
                    i.Status,
                    i.DueDate,
                    i.CreatedAt,
                    OrderTitle = i.Order.Title,
                    OrderId = i.OrderId
                })
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            Log.Information("Invoices retrieved for client {UserId}, count: {InvoiceCount}", userId, invoices.Count);
            return Success((object)invoices, "Invoices retrieved successfully");
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedOperationException("Invalid user context");
            }
            return userId;
        }
    }

    /// <summary>
    /// Request model for updating client profile
    /// </summary>
    public class UpdateProfileRequest
    {
        public string? Name { get; set; }
        public string? Company { get; set; }
        public string? Phone { get; set; }
    }
}