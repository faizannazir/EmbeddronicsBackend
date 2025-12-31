using System.Collections.Concurrent;
using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for session management service
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Force disconnect a user from all sessions
    /// </summary>
    Task<bool> ForceDisconnectUserAsync(int userId, string reason, int? disconnectedByAdminId = null);

    /// <summary>
    /// Force disconnect specific connection
    /// </summary>
    Task<bool> ForceDisconnectConnectionAsync(string connectionId, string reason, int? disconnectedByAdminId = null);

    /// <summary>
    /// Get all active sessions for a user
    /// </summary>
    Task<List<UserSessionDto>> GetUserSessionsAsync(int userId);

    /// <summary>
    /// Get all active sessions (admin view)
    /// </summary>
    Task<List<UserSessionDto>> GetAllActiveSessionsAsync();

    /// <summary>
    /// Block user from connecting
    /// </summary>
    Task BlockUserAsync(int userId, string reason, DateTime? blockUntil, int blockedByAdminId);

    /// <summary>
    /// Unblock user
    /// </summary>
    Task UnblockUserAsync(int userId, int unblockedByAdminId);

    /// <summary>
    /// Check if user is blocked
    /// </summary>
    Task<(bool IsBlocked, string? Reason, DateTime? BlockedUntil)> IsUserBlockedAsync(int userId);

    /// <summary>
    /// Get block history for user
    /// </summary>
    Task<List<UserBlockHistoryDto>> GetBlockHistoryAsync(int userId);
}

/// <summary>
/// DTO for user session information
/// </summary>
public class UserSessionDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public List<string> CurrentRooms { get; set; } = new();
    public TimeSpan SessionDuration => DateTime.UtcNow - ConnectedAt;
}

/// <summary>
/// DTO for user block history
/// </summary>
public class UserBlockHistoryDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
    public DateTime? BlockedUntil { get; set; }
    public int BlockedByAdminId { get; set; }
    public string BlockedByAdminName { get; set; } = string.Empty;
    public DateTime? UnblockedAt { get; set; }
    public int? UnblockedByAdminId { get; set; }
    public string? UnblockedByAdminName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Service for managing user sessions and providing admin control
/// </summary>
public class SessionManagementService : ISessionManagementService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<Hubs.ChatHub> _hubContext;
    private readonly IConnectionManagerService _connectionManager;

    // Track blocked users
    private static readonly ConcurrentDictionary<int, (string Reason, DateTime? BlockedUntil)> _blockedUsers = new();

    // Track session metadata
    private static readonly ConcurrentDictionary<string, SessionMetadata> _sessionMetadata = new();

    public SessionManagementService(
        IServiceScopeFactory scopeFactory,
        IHubContext<Hubs.ChatHub> hubContext,
        IConnectionManagerService connectionManager)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _connectionManager = connectionManager;
    }

    public async Task<bool> ForceDisconnectUserAsync(int userId, string reason, int? disconnectedByAdminId = null)
    {
        try
        {
            var connections = await _connectionManager.GetConnectionsForUserAsync(userId);
            
            if (!connections.Any())
            {
                Log.Information("No active connections found for user {UserId} to disconnect", userId);
                return true;
            }

            foreach (var connectionId in connections)
            {
                await ForceDisconnectConnectionInternalAsync(connectionId, userId, reason, disconnectedByAdminId);
            }

            // Log the admin action
            await LogAdminActionAsync(disconnectedByAdminId, "force_disconnect_user", 
                $"Force disconnected user {userId}: {reason}");

            Log.Information("Force disconnected all {Count} connections for user {UserId} by admin {AdminId}", 
                connections.Count(), userId, disconnectedByAdminId);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error force disconnecting user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ForceDisconnectConnectionAsync(string connectionId, string reason, int? disconnectedByAdminId = null)
    {
        try
        {
            // Get user ID for this connection by checking all online users
            var onlineUsers = await _connectionManager.GetOnlineUsersAsync();
            int userId = 0;
            
            foreach (var userPresence in onlineUsers)
            {
                var userConnections = await _connectionManager.GetConnectionsForUserAsync(userPresence.UserId);
                if (userConnections.Contains(connectionId))
                {
                    userId = userPresence.UserId;
                    break;
                }
            }

            await ForceDisconnectConnectionInternalAsync(connectionId, userId, reason, disconnectedByAdminId);

            // Log the admin action
            await LogAdminActionAsync(disconnectedByAdminId, "force_disconnect_connection", 
                $"Force disconnected connection {connectionId}: {reason}");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error force disconnecting connection {ConnectionId}", connectionId);
            return false;
        }
    }

    private async Task ForceDisconnectConnectionInternalAsync(string connectionId, int userId, string reason, int? adminId)
    {
        // Notify the client they're being disconnected
        await _hubContext.Clients.Client(connectionId).SendAsync("ForceDisconnect", new
        {
            Reason = reason,
            DisconnectedBy = adminId.HasValue ? "Administrator" : "System",
            Message = "Your session has been terminated. Please reconnect if this was unexpected."
        });

        // Give client a moment to receive the message
        await Task.Delay(500);

        // The actual disconnection happens when the client calls connection.stop()
        // or we can use SignalR's abort feature if available in the connection context
        
        // Remove from tracking - need userId and connectionId
        await _connectionManager.RemoveConnectionAsync(userId, connectionId);
        _sessionMetadata.TryRemove(connectionId, out _);

        Log.Information("Connection {ConnectionId} for user {UserId} force disconnected: {Reason}", 
            connectionId, userId, reason);
    }

    public async Task<List<UserSessionDto>> GetUserSessionsAsync(int userId)
    {
        var connections = await _connectionManager.GetConnectionsForUserAsync(userId);
        var sessions = new List<UserSessionDto>();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);

        foreach (var connectionId in connections)
        {
            var session = new UserSessionDto
            {
                ConnectionId = connectionId,
                UserId = userId,
                UserName = user?.Name ?? "Unknown",
                Email = user?.Email,
                Role = user?.Role ?? "unknown"
            };

            if (_sessionMetadata.TryGetValue(connectionId, out var metadata))
            {
                session.ConnectedAt = metadata.ConnectedAt;
                session.LastActivity = metadata.LastActivity;
                session.IpAddress = metadata.IpAddress;
                session.UserAgent = metadata.UserAgent;
                session.CurrentRooms = metadata.CurrentRooms.ToList();
            }

            sessions.Add(session);
        }

        return sessions;
    }

    public async Task<List<UserSessionDto>> GetAllActiveSessionsAsync()
    {
        var allUsers = await _connectionManager.GetOnlineUsersAsync();
        var sessions = new List<UserSessionDto>();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        foreach (var userPresence in allUsers)
        {
            var user = await context.Users.FindAsync(userPresence.UserId);
            var connections = await _connectionManager.GetConnectionsForUserAsync(userPresence.UserId);

            foreach (var connectionId in connections)
            {
                var session = new UserSessionDto
                {
                    ConnectionId = connectionId,
                    UserId = userPresence.UserId,
                    UserName = user?.Name ?? "Unknown",
                    Email = user?.Email,
                    Role = user?.Role ?? "unknown"
                };

                if (_sessionMetadata.TryGetValue(connectionId, out var metadata))
                {
                    session.ConnectedAt = metadata.ConnectedAt;
                    session.LastActivity = metadata.LastActivity;
                    session.IpAddress = metadata.IpAddress;
                    session.UserAgent = metadata.UserAgent;
                    session.CurrentRooms = metadata.CurrentRooms.ToList();
                }

                sessions.Add(session);
            }
        }

        return sessions.OrderByDescending(s => s.ConnectedAt).ToList();
    }

    public async Task BlockUserAsync(int userId, string reason, DateTime? blockUntil, int blockedByAdminId)
    {
        _blockedUsers[userId] = (reason, blockUntil);

        // Force disconnect the user immediately
        await ForceDisconnectUserAsync(userId, $"You have been blocked: {reason}", blockedByAdminId);

        // Log to database
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        context.UserActivityLogs.Add(new UserActivityLog
        {
            UserId = userId,
            ActivityType = "user_blocked",
            ActivityDetails = $"Blocked by admin {blockedByAdminId}. Reason: {reason}. Until: {blockUntil?.ToString() ?? "Indefinite"}",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        Log.Warning("User {UserId} blocked by admin {AdminId}: {Reason} until {Until}", 
            userId, blockedByAdminId, reason, blockUntil?.ToString() ?? "indefinite");
    }

    public async Task UnblockUserAsync(int userId, int unblockedByAdminId)
    {
        _blockedUsers.TryRemove(userId, out _);

        // Log to database
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        context.UserActivityLogs.Add(new UserActivityLog
        {
            UserId = userId,
            ActivityType = "user_unblocked",
            ActivityDetails = $"Unblocked by admin {unblockedByAdminId}",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        Log.Information("User {UserId} unblocked by admin {AdminId}", userId, unblockedByAdminId);
    }

    public Task<(bool IsBlocked, string? Reason, DateTime? BlockedUntil)> IsUserBlockedAsync(int userId)
    {
        if (_blockedUsers.TryGetValue(userId, out var blockInfo))
        {
            // Check if block has expired
            if (blockInfo.BlockedUntil.HasValue && blockInfo.BlockedUntil.Value < DateTime.UtcNow)
            {
                _blockedUsers.TryRemove(userId, out _);
                return Task.FromResult<(bool, string?, DateTime?)>((false, null, null));
            }

            return Task.FromResult<(bool, string?, DateTime?)>((true, blockInfo.Reason, blockInfo.BlockedUntil));
        }

        return Task.FromResult<(bool, string?, DateTime?)>((false, null, null));
    }

    public async Task<List<UserBlockHistoryDto>> GetBlockHistoryAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var blockLogs = await context.UserActivityLogs
            .Where(l => l.UserId == userId && 
                       (l.ActivityType == "user_blocked" || l.ActivityType == "user_unblocked"))
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .ToListAsync();

        var history = new List<UserBlockHistoryDto>();
        
        foreach (var log in blockLogs.Where(l => l.ActivityType == "user_blocked"))
        {
            var unblockLog = blockLogs.FirstOrDefault(l => 
                l.ActivityType == "user_unblocked" && 
                l.CreatedAt > log.CreatedAt);

            history.Add(new UserBlockHistoryDto
            {
                UserId = userId,
                Reason = log.ActivityDetails ?? "No reason provided",
                BlockedAt = log.CreatedAt,
                UnblockedAt = unblockLog?.CreatedAt,
                IsActive = unblockLog == null
            });
        }

        return history;
    }

    /// <summary>
    /// Register session metadata when user connects
    /// </summary>
    public static void RegisterSession(string connectionId, string? ipAddress, string? userAgent)
    {
        _sessionMetadata[connectionId] = new SessionMetadata
        {
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CurrentRooms = new HashSet<string>()
        };
    }

    /// <summary>
    /// Update session activity
    /// </summary>
    public static void UpdateSessionActivity(string connectionId)
    {
        if (_sessionMetadata.TryGetValue(connectionId, out var metadata))
        {
            metadata.LastActivity = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Add room to session
    /// </summary>
    public static void AddRoomToSession(string connectionId, string room)
    {
        if (_sessionMetadata.TryGetValue(connectionId, out var metadata))
        {
            metadata.CurrentRooms.Add(room);
        }
    }

    /// <summary>
    /// Remove room from session
    /// </summary>
    public static void RemoveRoomFromSession(string connectionId, string room)
    {
        if (_sessionMetadata.TryGetValue(connectionId, out var metadata))
        {
            metadata.CurrentRooms.Remove(room);
        }
    }

    /// <summary>
    /// Remove session metadata
    /// </summary>
    public static void RemoveSession(string connectionId)
    {
        _sessionMetadata.TryRemove(connectionId, out _);
    }

    private async Task LogAdminActionAsync(int? adminId, string actionType, string details)
    {
        if (!adminId.HasValue) return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        context.UserActivityLogs.Add(new UserActivityLog
        {
            UserId = adminId.Value,
            ActivityType = actionType,
            ActivityDetails = details,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private class SessionMetadata
    {
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public HashSet<string> CurrentRooms { get; set; } = new();
    }
}
