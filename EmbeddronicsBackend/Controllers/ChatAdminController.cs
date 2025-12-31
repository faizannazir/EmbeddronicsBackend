using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Services;
using System.Security.Claims;
using Serilog;

namespace EmbeddronicsBackend.Controllers;

/// <summary>
/// API controller for chat administration including session management, 
/// rate limiting, and user blocking.
/// </summary>
[ApiController]
[Route("api/admin/chat")]
[Authorize(Policy = "AdminOnly")]
public class ChatAdminController : ControllerBase
{
    private readonly ISessionManagementService _sessionManagement;
    private readonly IChatRateLimitService _rateLimitService;
    private readonly IChatAuthorizationService _authorizationService;
    private readonly IUserStatusService _userStatusService;

    public ChatAdminController(
        ISessionManagementService sessionManagement,
        IChatRateLimitService rateLimitService,
        IChatAuthorizationService authorizationService,
        IUserStatusService userStatusService)
    {
        _sessionManagement = sessionManagement;
        _rateLimitService = rateLimitService;
        _authorizationService = authorizationService;
        _userStatusService = userStatusService;
    }

    #region Session Management

    /// <summary>
    /// Get all active chat sessions
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllActiveSessions()
    {
        try
        {
            var sessions = await _sessionManagement.GetAllActiveSessionsAsync();
            return Ok(new
            {
                Success = true,
                TotalSessions = sessions.Count,
                Sessions = sessions
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting active sessions");
            return StatusCode(500, new { Success = false, Message = "Error retrieving sessions" });
        }
    }

    /// <summary>
    /// Get sessions for a specific user
    /// </summary>
    [HttpGet("sessions/user/{userId}")]
    public async Task<IActionResult> GetUserSessions(int userId)
    {
        try
        {
            var sessions = await _sessionManagement.GetUserSessionsAsync(userId);
            return Ok(new
            {
                Success = true,
                UserId = userId,
                TotalSessions = sessions.Count,
                Sessions = sessions
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting sessions for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error retrieving user sessions" });
        }
    }

    /// <summary>
    /// Force disconnect all sessions for a user
    /// </summary>
    [HttpPost("sessions/user/{userId}/disconnect")]
    public async Task<IActionResult> ForceDisconnectUser(int userId, [FromBody] ForceDisconnectRequest request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var result = await _sessionManagement.ForceDisconnectUserAsync(userId, request.Reason, adminId);

            if (result)
            {
                Log.Information("Admin {AdminId} force disconnected user {UserId}: {Reason}", 
                    adminId, userId, request.Reason);
                
                return Ok(new
                {
                    Success = true,
                    Message = $"User {userId} has been disconnected from all sessions"
                });
            }

            return BadRequest(new { Success = false, Message = "Failed to disconnect user" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error force disconnecting user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error disconnecting user" });
        }
    }

    /// <summary>
    /// Force disconnect a specific connection
    /// </summary>
    [HttpPost("sessions/connection/{connectionId}/disconnect")]
    public async Task<IActionResult> ForceDisconnectConnection(string connectionId, [FromBody] ForceDisconnectRequest request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var result = await _sessionManagement.ForceDisconnectConnectionAsync(connectionId, request.Reason, adminId);

            if (result)
            {
                Log.Information("Admin {AdminId} force disconnected connection {ConnectionId}: {Reason}", 
                    adminId, connectionId, request.Reason);
                
                return Ok(new
                {
                    Success = true,
                    Message = "Connection has been terminated"
                });
            }

            return BadRequest(new { Success = false, Message = "Failed to disconnect connection" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error force disconnecting connection {ConnectionId}", connectionId);
            return StatusCode(500, new { Success = false, Message = "Error disconnecting connection" });
        }
    }

    #endregion

    #region User Blocking

    /// <summary>
    /// Block a user from chat
    /// </summary>
    [HttpPost("users/{userId}/block")]
    public async Task<IActionResult> BlockUser(int userId, [FromBody] BlockUserRequest request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            
            if (!adminId.HasValue)
            {
                return Unauthorized(new { Success = false, Message = "Admin not authenticated" });
            }

            await _sessionManagement.BlockUserAsync(userId, request.Reason, request.BlockUntil, adminId.Value);

            Log.Warning("Admin {AdminId} blocked user {UserId}: {Reason} until {BlockUntil}", 
                adminId, userId, request.Reason, request.BlockUntil?.ToString() ?? "indefinite");

            return Ok(new
            {
                Success = true,
                Message = $"User {userId} has been blocked from chat",
                BlockedUntil = request.BlockUntil
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error blocking user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error blocking user" });
        }
    }

    /// <summary>
    /// Unblock a user from chat
    /// </summary>
    [HttpPost("users/{userId}/unblock")]
    public async Task<IActionResult> UnblockUser(int userId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            
            if (!adminId.HasValue)
            {
                return Unauthorized(new { Success = false, Message = "Admin not authenticated" });
            }

            await _sessionManagement.UnblockUserAsync(userId, adminId.Value);

            Log.Information("Admin {AdminId} unblocked user {UserId}", adminId, userId);

            return Ok(new
            {
                Success = true,
                Message = $"User {userId} has been unblocked"
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unblocking user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error unblocking user" });
        }
    }

    /// <summary>
    /// Check if a user is blocked
    /// </summary>
    [HttpGet("users/{userId}/block-status")]
    public async Task<IActionResult> GetBlockStatus(int userId)
    {
        try
        {
            var (isBlocked, reason, blockedUntil) = await _sessionManagement.IsUserBlockedAsync(userId);

            return Ok(new
            {
                Success = true,
                UserId = userId,
                IsBlocked = isBlocked,
                Reason = reason,
                BlockedUntil = blockedUntil
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting block status for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error retrieving block status" });
        }
    }

    /// <summary>
    /// Get block history for a user
    /// </summary>
    [HttpGet("users/{userId}/block-history")]
    public async Task<IActionResult> GetBlockHistory(int userId)
    {
        try
        {
            var history = await _sessionManagement.GetBlockHistoryAsync(userId);

            return Ok(new
            {
                Success = true,
                UserId = userId,
                History = history
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting block history for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error retrieving block history" });
        }
    }

    #endregion

    #region Rate Limiting

    /// <summary>
    /// Get rate limit status for a user
    /// </summary>
    [HttpGet("users/{userId}/rate-limit")]
    public async Task<IActionResult> GetRateLimitStatus(int userId)
    {
        try
        {
            var status = await _rateLimitService.GetRateLimitStatusAsync(userId);

            return Ok(new
            {
                Success = true,
                Status = status
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting rate limit status for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error retrieving rate limit status" });
        }
    }

    /// <summary>
    /// Reset rate limit for a user
    /// </summary>
    [HttpPost("users/{userId}/rate-limit/reset")]
    public async Task<IActionResult> ResetRateLimit(int userId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            await _rateLimitService.ResetRateLimitAsync(userId);

            Log.Information("Admin {AdminId} reset rate limit for user {UserId}", adminId, userId);

            return Ok(new
            {
                Success = true,
                Message = $"Rate limit reset for user {userId}"
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error resetting rate limit for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error resetting rate limit" });
        }
    }

    /// <summary>
    /// Set custom rate limit for a user
    /// </summary>
    [HttpPost("users/{userId}/rate-limit/configure")]
    public async Task<IActionResult> ConfigureRateLimit(int userId, [FromBody] ConfigureRateLimitRequest request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            await _rateLimitService.SetUserRateLimitAsync(userId, request.MessagesPerMinute, request.MessagesPerHour);

            Log.Information("Admin {AdminId} configured rate limit for user {UserId}: {PerMin}/min, {PerHour}/hour", 
                adminId, userId, request.MessagesPerMinute, request.MessagesPerHour);

            return Ok(new
            {
                Success = true,
                Message = $"Rate limit configured for user {userId}",
                NewLimits = new
                {
                    MessagesPerMinute = request.MessagesPerMinute,
                    MessagesPerHour = request.MessagesPerHour
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error configuring rate limit for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error configuring rate limit" });
        }
    }

    #endregion

    #region Authorization

    /// <summary>
    /// Get authorized rooms for a user
    /// </summary>
    [HttpGet("users/{userId}/authorized-rooms")]
    public async Task<IActionResult> GetAuthorizedRooms(int userId)
    {
        try
        {
            var rooms = await _authorizationService.GetAuthorizedRoomsAsync(userId);

            return Ok(new
            {
                Success = true,
                UserId = userId,
                AuthorizedRooms = rooms
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting authorized rooms for user {UserId}", userId);
            return StatusCode(500, new { Success = false, Message = "Error retrieving authorized rooms" });
        }
    }

    #endregion

    #region Helper Methods

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                       ?? User.FindFirst("userId")?.Value;
        
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    #endregion
}

#region Request DTOs

public class ForceDisconnectRequest
{
    public string Reason { get; set; } = "Session terminated by administrator";
}

public class BlockUserRequest
{
    public string Reason { get; set; } = string.Empty;
    public DateTime? BlockUntil { get; set; }
}

public class ConfigureRateLimitRequest
{
    public int MessagesPerMinute { get; set; } = 20;
    public int MessagesPerHour { get; set; } = 200;
}

#endregion
