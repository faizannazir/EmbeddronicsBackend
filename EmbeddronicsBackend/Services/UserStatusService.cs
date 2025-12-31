using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Service for admin user status monitoring and activity tracking
/// </summary>
public class UserStatusService : IUserStatusService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionManagerService _connectionManager;

    public UserStatusService(
        IServiceScopeFactory scopeFactory,
        IConnectionManagerService connectionManager)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
    }

    public async Task<List<AdminUserStatusDto>> GetAllUsersStatusAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var users = await context.Users
            .Where(u => u.Role == "client")
            .Select(u => new AdminUserStatusDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Email = u.Email,
                Company = u.Company,
                Role = u.Role,
                Status = u.Status
            })
            .ToListAsync();

        // Get online users
        var onlineUsers = await _connectionManager.GetOnlineUsersAsync();
        var onlineUserIds = onlineUsers.Select(u => u.UserId).ToHashSet();

        foreach (var user in users)
        {
            user.IsOnline = onlineUserIds.Contains(user.UserId);
            
            var onlineUser = onlineUsers.FirstOrDefault(u => u.UserId == user.UserId);
            if (onlineUser != null)
            {
                user.LastSeenAt = onlineUser.LastSeenAt;
                user.CurrentChatRoom = onlineUser.CurrentChatRoom;
            }

            // Get message counts
            user.TotalMessagesSent = await context.ChatMessages
                .CountAsync(m => m.SenderId == user.UserId && !m.IsDeleted);

            user.TotalMessagesReceived = await context.ChatMessages
                .CountAsync(m => m.RecipientId == user.UserId && !m.IsDeleted);

            user.UnreadMessagesCount = await context.ChatMessages
                .CountAsync(m => m.RecipientId == user.UserId && !m.IsRead && !m.IsDeleted);

            // Get active conversations
            user.ActiveConversations = await context.ChatMessages
                .Where(m => (m.SenderId == user.UserId || m.RecipientId == user.UserId) && !m.IsDeleted)
                .Select(m => m.ChatRoom)
                .Distinct()
                .CountAsync();

            // Get last login from activity log
            var lastLogin = await context.UserActivityLogs
                .Where(a => a.UserId == user.UserId && a.ActivityType == "login")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            user.LastLoginAt = lastLogin?.CreatedAt;

            // Get recent activities (limited for performance)
            user.RecentActivities = await GetRecentActivitiesAsync(user.UserId, 5);
        }

        return users.OrderByDescending(u => u.IsOnline)
                   .ThenByDescending(u => u.LastSeenAt)
                   .ToList();
    }

    public async Task<AdminUserStatusDto?> GetUserStatusAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => new AdminUserStatusDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Email = u.Email,
                Company = u.Company,
                Role = u.Role,
                Status = u.Status
            })
            .FirstOrDefaultAsync();

        if (user == null) return null;

        // Get online status
        var presence = await _connectionManager.GetUserPresenceAsync(userId);
        if (presence != null)
        {
            user.IsOnline = presence.IsOnline;
            user.LastSeenAt = presence.LastSeenAt;
            user.CurrentChatRoom = presence.CurrentChatRoom;
        }

        // Get message counts
        user.TotalMessagesSent = await context.ChatMessages
            .CountAsync(m => m.SenderId == userId && !m.IsDeleted);

        user.TotalMessagesReceived = await context.ChatMessages
            .CountAsync(m => m.RecipientId == userId && !m.IsDeleted);

        user.UnreadMessagesCount = await context.ChatMessages
            .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.IsDeleted);

        // Get active conversations count
        user.ActiveConversations = await context.ChatMessages
            .Where(m => (m.SenderId == userId || m.RecipientId == userId) && !m.IsDeleted)
            .Select(m => m.ChatRoom)
            .Distinct()
            .CountAsync();

        // Get last login
        var lastLogin = await context.UserActivityLogs
            .Where(a => a.UserId == userId && a.ActivityType == "login")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        user.LastLoginAt = lastLogin?.CreatedAt;

        // Get recent activities
        user.RecentActivities = await GetRecentActivitiesAsync(userId, 20);

        // Get open conversations
        user.OpenConversations = await GetUserConversationsAsync(userId);

        return user;
    }

    public async Task<List<UserPresenceDto>> GetOnlineUsersAsync()
    {
        return await _connectionManager.GetOnlineUsersAsync();
    }

    public async Task<List<AdminUserStatusDto>> GetUsersWithUnreadMessagesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var usersWithUnread = await context.ChatMessages
            .Where(m => !m.IsRead && !m.IsDeleted && m.RecipientId != null)
            .Select(m => m.RecipientId!.Value)
            .Distinct()
            .ToListAsync();

        var users = await context.Users
            .Where(u => usersWithUnread.Contains(u.Id))
            .Select(u => new AdminUserStatusDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Email = u.Email,
                Company = u.Company,
                Role = u.Role,
                Status = u.Status
            })
            .ToListAsync();

        foreach (var user in users)
        {
            user.UnreadMessagesCount = await context.ChatMessages
                .CountAsync(m => m.RecipientId == user.UserId && !m.IsRead && !m.IsDeleted);

            var presence = await _connectionManager.GetUserPresenceAsync(user.UserId);
            if (presence != null)
            {
                user.IsOnline = presence.IsOnline;
                user.LastSeenAt = presence.LastSeenAt;
            }
        }

        return users.OrderByDescending(u => u.UnreadMessagesCount).ToList();
    }

    public async Task LogActivityAsync(
        int userId, 
        string activityType, 
        string? details = null, 
        string? chatRoom = null, 
        string? ipAddress = null, 
        string? userAgent = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var activity = new UserActivityLog
        {
            UserId = userId,
            ActivityType = activityType,
            ActivityDetails = details,
            ChatRoom = chatRoom,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };

        context.UserActivityLogs.Add(activity);
        await context.SaveChangesAsync();

        Log.Debug("User activity logged: {UserId} - {ActivityType}", userId, activityType);
    }

    public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int userId, int limit = 20)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        return await context.UserActivityLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new RecentActivityDto
            {
                ActivityType = a.ActivityType,
                Description = a.ActivityDetails,
                ChatRoom = a.ChatRoom,
                Timestamp = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task UpdateLastSeenAsync(int userId)
    {
        await _connectionManager.UpdateUserActivityAsync(userId);
    }

    public async Task<Dictionary<string, int>> GetReadStatusSummaryAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var summary = new Dictionary<string, int>();

        // Messages sent by user that have been read
        summary["sentAndRead"] = await context.ChatMessages
            .CountAsync(m => m.SenderId == userId && m.IsRead && !m.IsDeleted);

        // Messages sent by user that are unread
        summary["sentUnread"] = await context.ChatMessages
            .CountAsync(m => m.SenderId == userId && !m.IsRead && !m.IsDeleted);

        // Messages received that user has read
        summary["receivedAndRead"] = await context.ChatMessages
            .CountAsync(m => m.RecipientId == userId && m.IsRead && !m.IsDeleted);

        // Messages received that are unread
        summary["receivedUnread"] = await context.ChatMessages
            .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.IsDeleted);

        return summary;
    }

    public async Task<ConversationStatsDto> GetConversationStatsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var stats = new ConversationStatsDto();

        // Total unique chat rooms
        stats.TotalConversations = await context.ChatMessages
            .Where(m => !m.IsDeleted)
            .Select(m => m.ChatRoom)
            .Distinct()
            .CountAsync();

        // Active conversations (with messages in last 24 hours)
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        stats.ActiveConversations = await context.ChatMessages
            .Where(m => !m.IsDeleted && m.CreatedAt >= oneDayAgo)
            .Select(m => m.ChatRoom)
            .Distinct()
            .CountAsync();

        // Total messages
        stats.TotalMessages = await context.ChatMessages
            .CountAsync(m => !m.IsDeleted);

        // Unread messages
        stats.UnreadMessages = await context.ChatMessages
            .CountAsync(m => !m.IsRead && !m.IsDeleted);

        // Online users
        var onlineUsers = await _connectionManager.GetOnlineUsersAsync();
        stats.OnlineUsers = onlineUsers.Count;

        // Total users
        stats.TotalUsers = await context.Users.CountAsync(u => u.Status == "active");

        // Top active rooms
        stats.TopActiveRooms = await context.ChatMessages
            .Where(m => !m.IsDeleted)
            .GroupBy(m => m.ChatRoom)
            .Select(g => new ChatRoomStatsDto
            {
                RoomId = g.Key,
                RoomName = g.Key, // Would need additional logic to get proper names
                MessageCount = g.Count(),
                LastActivity = g.Max(m => m.CreatedAt)
            })
            .OrderByDescending(r => r.MessageCount)
            .Take(10)
            .ToListAsync();

        return stats;
    }

    private async Task<List<ConversationSummaryDto>> GetUserConversationsAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var chatRooms = await context.ChatMessages
            .Where(m => (m.SenderId == userId || m.RecipientId == userId) && !m.IsDeleted)
            .Select(m => m.ChatRoom)
            .Distinct()
            .ToListAsync();

        var conversations = new List<ConversationSummaryDto>();

        foreach (var room in chatRooms)
        {
            var lastMessage = await context.ChatMessages
                .Where(m => m.ChatRoom == room && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            var unreadCount = await context.ChatMessages
                .CountAsync(m => m.ChatRoom == room && 
                               m.RecipientId == userId && 
                               !m.IsRead && 
                               !m.IsDeleted);

            conversations.Add(new ConversationSummaryDto
            {
                ChatRoom = room,
                RoomName = room, // Would need lookup for proper name
                UnreadCount = unreadCount,
                LastMessageAt = lastMessage?.CreatedAt,
                LastMessagePreview = lastMessage?.Content.Length > 50 
                    ? lastMessage.Content.Substring(0, 50) + "..." 
                    : lastMessage?.Content
            });
        }

        return conversations.OrderByDescending(c => c.LastMessageAt).ToList();
    }
}
