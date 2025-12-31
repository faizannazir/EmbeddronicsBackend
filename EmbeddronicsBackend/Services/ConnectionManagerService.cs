using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Service for managing SignalR connections and user presence.
/// Uses both in-memory tracking for performance and database persistence for reliability.
/// </summary>
public class ConnectionManagerService : IConnectionManagerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    // In-memory cache for fast lookups
    private static readonly ConcurrentDictionary<int, HashSet<string>> _userConnections = new();
    private static readonly ConcurrentDictionary<string, int> _connectionToUser = new();
    private static readonly ConcurrentDictionary<int, (DateTime LastActivity, string? CurrentRoom)> _userActivity = new();

    public ConnectionManagerService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task AddConnectionAsync(int userId, string connectionId, string? userAgent = null, string? ipAddress = null)
    {
        // Update in-memory cache
        _userConnections.AddOrUpdate(
            userId,
            _ => new HashSet<string> { connectionId },
            (_, connections) =>
            {
                connections.Add(connectionId);
                return connections;
            });

        _connectionToUser[connectionId] = userId;
        _userActivity[userId] = (DateTime.UtcNow, null);

        // Persist to database
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var userConnection = new UserConnection
        {
            UserId = userId,
            ConnectionId = connectionId,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        context.UserConnections.Add(userConnection);
        await context.SaveChangesAsync();

        Log.Debug("Added connection {ConnectionId} for user {UserId}", connectionId, userId);
    }

    public async Task RemoveConnectionAsync(int userId, string connectionId)
    {
        // Update in-memory cache
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);
                _userActivity.TryRemove(userId, out _);
            }
        }

        _connectionToUser.TryRemove(connectionId, out _);

        // Update database
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var connection = await context.UserConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == connectionId && c.UserId == userId);

        if (connection != null)
        {
            connection.IsConnected = false;
            connection.DisconnectedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        Log.Debug("Removed connection {ConnectionId} for user {UserId}", connectionId, userId);
    }

    public Task<bool> IsUserOnlineAsync(int userId)
    {
        var isOnline = _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        return Task.FromResult(isOnline);
    }

    public Task<List<string>> GetConnectionsForUserAsync(int userId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            return Task.FromResult(connections.ToList());
        }
        return Task.FromResult(new List<string>());
    }

    public async Task<List<UserPresenceDto>> GetOnlineUsersAsync()
    {
        var onlineUserIds = _userConnections.Keys.ToList();
        
        if (onlineUserIds.Count == 0)
            return new List<UserPresenceDto>();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var users = await context.Users
            .Where(u => onlineUserIds.Contains(u.Id))
            .Select(u => new UserPresenceDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Role = u.Role,
                IsOnline = true
            })
            .ToListAsync();

        // Add last activity info from cache
        foreach (var user in users)
        {
            if (_userActivity.TryGetValue(user.UserId, out var activity))
            {
                user.LastSeenAt = activity.LastActivity;
                user.CurrentChatRoom = activity.CurrentRoom;
            }
        }

        return users;
    }

    public async Task<List<UserPresenceDto>> GetOnlineUsersInRoomAsync(string chatRoom)
    {
        var usersInRoom = _userActivity
            .Where(kvp => kvp.Value.CurrentRoom == chatRoom)
            .Select(kvp => kvp.Key)
            .ToList();

        if (usersInRoom.Count == 0)
            return new List<UserPresenceDto>();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        return await context.Users
            .Where(u => usersInRoom.Contains(u.Id))
            .Select(u => new UserPresenceDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Role = u.Role,
                IsOnline = true,
                CurrentChatRoom = chatRoom
            })
            .ToListAsync();
    }

    public Task UpdateUserActivityAsync(int userId, string? currentChatRoom = null)
    {
        _userActivity.AddOrUpdate(
            userId,
            _ => (DateTime.UtcNow, currentChatRoom),
            (_, _) => (DateTime.UtcNow, currentChatRoom));

        return Task.CompletedTask;
    }

    public async Task<UserPresenceDto?> GetUserPresenceAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserPresenceDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Role = u.Role
            })
            .FirstOrDefaultAsync();

        if (user == null) return null;

        user.IsOnline = _userConnections.ContainsKey(userId);
        
        if (_userActivity.TryGetValue(userId, out var activity))
        {
            user.LastSeenAt = activity.LastActivity;
            user.CurrentChatRoom = activity.CurrentRoom;
        }

        return user;
    }

    public async Task CleanupStaleConnectionsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Mark all connections as disconnected that haven't been explicitly disconnected
        var staleConnections = await context.UserConnections
            .Where(c => c.IsConnected && c.LastActivityAt < DateTime.UtcNow.AddHours(-24))
            .ToListAsync();

        foreach (var connection in staleConnections)
        {
            connection.IsConnected = false;
            connection.DisconnectedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        // Clear in-memory cache
        _userConnections.Clear();
        _connectionToUser.Clear();
        _userActivity.Clear();

        Log.Information("Cleaned up {Count} stale connections", staleConnections.Count);
    }
}
