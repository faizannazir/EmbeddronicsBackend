using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Authorization.Attributes;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all actions
public class AdminController : BaseApiController
{
    private readonly EmbeddronicsDbContext _context;
    private readonly IUserRegistrationService _registrationService;
    private readonly IOrderService _orderService;
    private readonly IQuoteService _quoteService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        EmbeddronicsDbContext context, 
        IUserRegistrationService registrationService,
        IOrderService orderService,
        IQuoteService quoteService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _registrationService = registrationService;
        _orderService = orderService;
        _quoteService = quoteService;
        _logger = logger;
    }

    /// <summary>
    /// Get admin dashboard overview with key metrics
    /// </summary>
    [HttpGet("dashboard")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> GetDashboard()
    {
        try
        {
            var dashboard = new
            {
                TotalClients = await _context.Users.CountAsync(u => u.Role == "client"),
                ActiveOrders = await _context.Orders.CountAsync(o => o.Status == "in_progress"),
                PendingQuotes = await _context.Quotes.CountAsync(q => q.Status == "pending"),
                TotalRevenue = await _context.Invoices.Where(i => i.Status == "paid").SumAsync(i => i.Amount),
                RecentOrders = await _context.Orders
                    .Include(o => o.Client)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .Select(o => new
                    {
                        o.Id,
                        o.Title,
                        o.Status,
                        ClientName = o.Client.Name,
                        o.CreatedAt
                    })
                    .ToListAsync(),
                RecentClients = await _context.Users
                    .Where(u => u.Role == "client")
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.Company,
                        u.Status,
                        u.CreatedAt
                    })
                    .ToListAsync()
            };

            return Success((object)dashboard, "Dashboard data retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin dashboard");
            return InternalServerError<object>("Failed to retrieve dashboard data");
        }
    }

    /// <summary>
    /// Get all clients with filtering and pagination
    /// </summary>
    [HttpGet("clients")]
    [ManageClients] // Admin-only access for managing clients/users
    public async Task<ActionResult<ApiResponse<object>>> GetClients(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Users.Where(u => u.Role == "client");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Name.Contains(search) || 
                                       u.Email.Contains(search) || 
                                       u.Company.Contains(search));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(u => u.Status == status);
            }

            var totalCount = await query.CountAsync();
            var clients = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Name,
                    u.Role,
                    u.Status,
                    u.Company,
                    u.Phone,
                    u.CreatedAt,
                    OrderCount = u.Orders.Count(),
                    LastOrderDate = u.Orders.Any() ? u.Orders.OrderByDescending(o => o.CreatedAt).First().CreatedAt : (DateTime?)null
                })
                .ToListAsync();

            var result = new
            {
                Clients = clients,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Success((object)result, "Clients retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients");
            return InternalServerError<object>("Failed to retrieve clients");
        }
    }

    /// <summary>
    /// Get specific client details
    /// </summary>
    [HttpGet("clients/{id}")]
    [ManageClients] // Admin-only access for managing clients
    public async Task<ActionResult<ApiResponse<object>>> GetClient(int id)
    {
        try
        {
            var client = await _context.Users
                .Include(u => u.Orders)
                .Include(u => u.Quotes)
                .Where(u => u.Id == id && u.Role == "client")
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Name,
                    u.Company,
                    u.Phone,
                    u.Status,
                    u.CreatedAt,
                    u.UpdatedAt,
                    Orders = u.Orders.Select(o => new
                    {
                        o.Id,
                        o.Title,
                        o.Status,
                        o.BudgetRange,
                        o.CreatedAt
                    }),
                    Quotes = u.Quotes.Select(q => new
                    {
                        q.Id,
                        q.Amount,
                        q.Status,
                        q.CreatedAt
                    })
                })
                .FirstOrDefaultAsync();

            if (client == null)
            {
                return NotFound<object>("Client not found");
            }

            return Success((object)client, "Client details retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientId}", id);
            return InternalServerError<object>("Failed to retrieve client details");
        }
    }

    /// <summary>
    /// Update client status (approve/suspend/activate)
    /// </summary>
    [HttpPut("clients/{id}/status")]
    [ManageClients] // Admin-only access for managing clients
    public async Task<ActionResult<ApiResponse<object>>> UpdateClientStatus(int id, [FromBody] UpdateClientStatusRequest request)
    {
        try
        {
            var client = await _context.Users.FindAsync(id);
            if (client == null || client.Role != "client")
            {
                return NotFound<object>("Client not found");
            }

            client.Status = request.Status;
            client.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Client {ClientId} status updated to {Status} by admin", id, request.Status);
            return Success((object)new { message = "Client status updated successfully" }, "Client status updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client {ClientId} status", id);
            return InternalServerError<object>("Failed to update client status");
        }
    }

    /// <summary>
    /// Get all orders with filtering and pagination
    /// </summary>
    [HttpGet("orders")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> GetOrders(
        [FromQuery] string? status = null,
        [FromQuery] int? clientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var orders = await _orderService.GetOrdersAsync(clientId);
            
            var filteredOrders = orders.AsQueryable();
            if (!string.IsNullOrEmpty(status))
            {
                filteredOrders = filteredOrders.Where(o => o.Status == status);
            }

            var totalCount = filteredOrders.Count();
            var pagedOrders = filteredOrders
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new
            {
                Orders = pagedOrders,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Success((object)result, "Orders retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders");
            return InternalServerError<object>("Failed to retrieve orders");
        }
    }

    /// <summary>
    /// Update order status
    /// </summary>
    [HttpPut("orders/{id}/status")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            // Get current user ID for authorization
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return BadRequest<object>("Invalid user context");
            }

            var result = await _orderService.UpdateOrderStatusAsync(id, request.Status, currentUserId);
            if (result)
            {
                return Success((object)new { message = "Order status updated successfully" }, "Order status updated successfully");
            }
            else
            {
                return NotFound<object>("Order not found or update failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status", id);
            return InternalServerError<object>("Failed to update order status");
        }
    }

    /// <summary>
    /// Get all quotes with filtering and pagination
    /// </summary>
    [HttpGet("quotes")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> GetQuotes(
        [FromQuery] string? status = null,
        [FromQuery] int? clientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Quotes
                .Include(q => q.Client)
                .Include(q => q.Order)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(q => q.Status == status);
            }

            if (clientId.HasValue)
            {
                query = query.Where(q => q.ClientId == clientId.Value);
            }

            var totalCount = await query.CountAsync();
            var quotes = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.Amount,
                    q.Currency,
                    q.Status,
                    q.ValidUntil,
                    q.CreatedAt,
                    ClientName = q.Client.Name,
                    OrderTitle = q.Order != null ? q.Order.Title : "Unknown Order"
                })
                .ToListAsync();

            var result = new
            {
                Quotes = quotes,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Success((object)result, "Quotes retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quotes");
            return InternalServerError<object>("Failed to retrieve quotes");
        }
    }

    /// <summary>
    /// Create a new quote for an order
    /// </summary>
    [HttpPost("quotes")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> CreateQuote([FromBody] CreateQuoteRequest request)
    {
        try
        {
            // Get current user ID for authorization
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return BadRequest<object>("Invalid user context");
            }

            var quote = await _quoteService.CreateQuoteAsync(request, currentUserId);
            return Success((object)quote, "Quote created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quote for order {OrderId}", request.OrderId);
            return InternalServerError<object>("Failed to create quote");
        }
    }

    /// <summary>
    /// Update quote details
    /// </summary>
    [HttpPut("quotes/{id}")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> UpdateQuote(int id, [FromBody] UpdateQuoteRequest request)
    {
        try
        {
            // Get current user ID for authorization
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return BadRequest<object>("Invalid user context");
            }

            var quote = await _quoteService.UpdateQuoteAsync(id, request, currentUserId);
            if (quote != null)
            {
                return Success((object)quote, "Quote updated successfully");
            }
            else
            {
                return NotFound<object>("Quote not found or update failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quote {QuoteId}", id);
            return InternalServerError<object>("Failed to update quote");
        }
    }

    /// <summary>
    /// Get leads (potential clients who submitted contact forms)
    /// </summary>
    [HttpGet("leads")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> GetLeads()
    {
        try
        {
            // This would typically come from a Leads table
            // For now, return empty array as leads functionality would need to be implemented
            var leads = new List<object>();
            
            return Success((object)leads, "Leads retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leads");
            return InternalServerError<object>("Failed to retrieve leads");
        }
    }

    /// <summary>
    /// Get reviews for approval
    /// </summary>
    [HttpGet("reviews")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> GetReviews()
    {
        try
        {
            // This would typically come from a Reviews table
            // For now, return empty array as reviews functionality would need to be implemented
            var reviews = new List<object>();
            
            return Success((object)reviews, "Reviews retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews");
            return InternalServerError<object>("Failed to retrieve reviews");
        }
    }

    [HttpGet("registration-settings")]
    [AdminCRM] // Admin CRM access required
    public ActionResult<ApiResponse<object>> GetRegistrationSettings()
    {
        var settings = new
        {
            IsAdminRegistrationEnabled = _registrationService.IsAdminRegistrationEnabled,
            DefaultRole = _registrationService.GetDefaultRoleForNewUsers(),
            DefaultStatus = _registrationService.GetDefaultStatusForNewUsers()
        };

        return Success((object)settings, "Registration settings retrieved successfully");
    }

    [HttpGet("test-seeding")]
    [AdminCRM] // Admin CRM access required
    public async Task<ActionResult<ApiResponse<object>>> TestSeeding()
    {
        var adminCount = await _context.Users.CountAsync(u => u.Role == "admin");
        var adminEmails = await _context.Users
            .Where(u => u.Role == "admin")
            .Select(u => u.Email)
            .ToListAsync();

        var result = new
        {
            AdminCount = adminCount,
            AdminEmails = adminEmails,
            SeedingStatus = adminCount > 0 ? "Success" : "Failed"
        };

        return Success((object)result, "Seeding test completed");
    }
}

// Request DTOs for admin operations
public class UpdateClientStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
}