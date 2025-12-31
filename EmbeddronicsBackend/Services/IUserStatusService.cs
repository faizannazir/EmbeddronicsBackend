using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for admin user status and activity monitoring
/// </summary>
public interface IUserStatusService
{
    /// <summary>
    /// Get detailed status for all users (admin view)
    /// </summary>
    Task<List<AdminUserStatusDto>> GetAllUsersStatusAsync();

    /// <summary>
    /// Get detailed status for a specific user
    /// </summary>
    Task<AdminUserStatusDto?> GetUserStatusAsync(int userId);

    /// <summary>
    /// Get users currently online
    /// </summary>
    Task<List<UserPresenceDto>> GetOnlineUsersAsync();

    /// <summary>
    /// Get users with unread messages
    /// </summary>
    Task<List<AdminUserStatusDto>> GetUsersWithUnreadMessagesAsync();

    /// <summary>
    /// Log user activity
    /// </summary>
    Task LogActivityAsync(int userId, string activityType, string? details = null, string? chatRoom = null, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Get recent activities for a user
    /// </summary>
    Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int userId, int limit = 20);

    /// <summary>
    /// Update user's last seen timestamp
    /// </summary>
    Task UpdateLastSeenAsync(int userId);

    /// <summary>
    /// Get message read status summary for a user
    /// </summary>
    Task<Dictionary<string, int>> GetReadStatusSummaryAsync(int userId);

    /// <summary>
    /// Get conversation statistics for admin dashboard
    /// </summary>
    Task<ConversationStatsDto> GetConversationStatsAsync();
}

/// <summary>
/// DTO for conversation statistics
/// </summary>
public class ConversationStatsDto
{
    public int TotalConversations { get; set; }
    public int ActiveConversations { get; set; }
    public int TotalMessages { get; set; }
    public int UnreadMessages { get; set; }
    public int OnlineUsers { get; set; }
    public int TotalUsers { get; set; }
    public double AverageResponseTimeMinutes { get; set; }
    public List<ChatRoomStatsDto> TopActiveRooms { get; set; } = new();
}

/// <summary>
/// DTO for chat room statistics
/// </summary>
public class ChatRoomStatsDto
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public int ParticipantCount { get; set; }
    public DateTime? LastActivity { get; set; }
}
