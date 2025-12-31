using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for managing SignalR connections and user presence
/// </summary>
public interface IConnectionManagerService
{
    /// <summary>
    /// Add a new connection for a user
    /// </summary>
    Task AddConnectionAsync(int userId, string connectionId, string? userAgent = null, string? ipAddress = null);

    /// <summary>
    /// Remove a connection for a user
    /// </summary>
    Task RemoveConnectionAsync(int userId, string connectionId);

    /// <summary>
    /// Check if a user is currently online (has active connections)
    /// </summary>
    Task<bool> IsUserOnlineAsync(int userId);

    /// <summary>
    /// Get all connection IDs for a specific user
    /// </summary>
    Task<List<string>> GetConnectionsForUserAsync(int userId);

    /// <summary>
    /// Get all online users
    /// </summary>
    Task<List<UserPresenceDto>> GetOnlineUsersAsync();

    /// <summary>
    /// Get online users in a specific chat room
    /// </summary>
    Task<List<UserPresenceDto>> GetOnlineUsersInRoomAsync(string chatRoom);

    /// <summary>
    /// Update user's last activity timestamp
    /// </summary>
    Task UpdateUserActivityAsync(int userId, string? currentChatRoom = null);

    /// <summary>
    /// Get presence information for a specific user
    /// </summary>
    Task<UserPresenceDto?> GetUserPresenceAsync(int userId);

    /// <summary>
    /// Cleanup stale connections (e.g., for server restarts)
    /// </summary>
    Task CleanupStaleConnectionsAsync();
}
