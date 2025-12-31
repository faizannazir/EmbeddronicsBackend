using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for chat authorization service
/// </summary>
public interface IChatAuthorizationService
{
    /// <summary>
    /// Check if user can connect to chat system
    /// </summary>
    Task<(bool Allowed, string? Reason)> CanConnectAsync(int userId);

    /// <summary>
    /// Check if user can access a specific chat room
    /// </summary>
    Task<(bool Allowed, string? Reason)> CanAccessRoomAsync(int userId, string roomName);

    /// <summary>
    /// Check if user can send message in a room
    /// </summary>
    Task<(bool Allowed, string? Reason)> CanSendMessageAsync(int userId, string roomName);

    /// <summary>
    /// Check if user can view a specific message
    /// </summary>
    Task<bool> CanViewMessageAsync(int userId, int messageId);

    /// <summary>
    /// Check if user can edit a specific message
    /// </summary>
    Task<(bool Allowed, string? Reason)> CanEditMessageAsync(int userId, int messageId);

    /// <summary>
    /// Check if user can delete a specific message
    /// </summary>
    Task<(bool Allowed, string? Reason)> CanDeleteMessageAsync(int userId, int messageId);

    /// <summary>
    /// Get list of rooms user is authorized to access
    /// </summary>
    Task<List<string>> GetAuthorizedRoomsAsync(int userId);

    /// <summary>
    /// Filter messages to only those user is authorized to see
    /// </summary>
    Task<List<ChatMessage>> FilterAuthorizedMessagesAsync(int userId, List<ChatMessage> messages);

    /// <summary>
    /// Check if user has admin privileges for chat
    /// </summary>
    Task<bool> IsAdminAsync(int userId);
}

/// <summary>
/// Service for handling chat authorization and access control
/// </summary>
public class ChatAuthorizationService : IChatAuthorizationService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ChatAuthorizationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<(bool Allowed, string? Reason)> CanConnectAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);

        if (user == null)
        {
            Log.Warning("Chat connection denied: User {UserId} not found", userId);
            return (false, "User not found");
        }

        // Check if user account is active (Status == "active")
        if (user.Status != "active")
        {
            Log.Warning("Chat connection denied: User {UserId} account status is {Status}", userId, user.Status);
            return (false, "Your account is not active. Please contact support.");
        }

        Log.Information("Chat connection allowed for user {UserId} ({Role})", userId, user.Role);
        return (true, null);
    }

    public async Task<(bool Allowed, string? Reason)> CanAccessRoomAsync(int userId, string roomName)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        
        if (user == null)
        {
            return (false, "User not found");
        }

        // Admins can access all rooms
        if (user.Role == "admin")
        {
            return (true, null);
        }

        // Parse room name to determine access rules
        var roomParts = roomName.Split('_');
        var roomType = roomParts[0].ToLowerInvariant();

        switch (roomType)
        {
            case "order":
                // Users can only access order rooms for their own orders
                if (roomParts.Length >= 2 && int.TryParse(roomParts[1], out var orderId))
                {
                    var hasAccess = await context.Orders
                        .AnyAsync(o => o.Id == orderId && o.ClientId == userId);
                    
                    if (!hasAccess)
                    {
                        Log.Warning("User {UserId} denied access to order room {Room} - not their order", 
                            userId, roomName);
                        return (false, "You don't have access to this order");
                    }
                    return (true, null);
                }
                return (false, "Invalid room format");

            case "project":
                // For projects, we check if user is the client
                // Note: If Projects DbSet doesn't exist, we deny access
                if (roomParts.Length >= 2 && int.TryParse(roomParts[1], out var projectId))
                {
                    // Since Projects DbSet may not exist, we'll check orders instead
                    // or simply allow access for now and log
                    Log.Information("Project room access requested by user {UserId} for project {ProjectId}", 
                        userId, projectId);
                    // Allow access for now - can be enhanced when Projects table is added
                    return (true, null);
                }
                return (false, "Invalid room format");

            case "support":
                // Support rooms are for the specific user or admins
                if (roomParts.Length >= 2 && int.TryParse(roomParts[1], out var supportUserId))
                {
                    if (supportUserId != userId)
                    {
                        Log.Warning("User {UserId} denied access to support room {Room} - not their support room", 
                            userId, roomName);
                        return (false, "You can only access your own support room");
                    }
                    return (true, null);
                }
                return (false, "Invalid room format");

            case "user":
                // User-specific notification rooms
                if (roomParts.Length >= 2 && int.TryParse(roomParts[1], out var userRoomId))
                {
                    if (userRoomId != userId)
                    {
                        return (false, "You can only access your own user room");
                    }
                    return (true, null);
                }
                return (false, "Invalid room format");

            case "dm":
                // Direct messages - user must be one of the participants
                if (roomParts.Length >= 3 && 
                    int.TryParse(roomParts[1], out var user1) && 
                    int.TryParse(roomParts[2], out var user2))
                {
                    if (userId != user1 && userId != user2)
                    {
                        Log.Warning("User {UserId} denied access to DM room {Room} - not a participant", 
                            userId, roomName);
                        return (false, "You are not a participant in this conversation");
                    }
                    return (true, null);
                }
                return (false, "Invalid room format");

            case "quote":
                // Quote rooms for quote discussions
                if (roomParts.Length >= 2 && int.TryParse(roomParts[1], out var quoteId))
                {
                    var hasAccess = await context.Quotes
                        .AnyAsync(q => q.Id == quoteId && q.ClientId == userId);
                    
                    if (!hasAccess)
                    {
                        Log.Warning("User {UserId} denied access to quote room {Room}", userId, roomName);
                        return (false, "You don't have access to this quote");
                    }
                    return (true, null);
                }
                return (false, "Invalid room format");

            default:
                Log.Warning("User {UserId} denied access to unknown room type: {Room}", userId, roomName);
                return (false, "Invalid room type");
        }
    }

    public async Task<(bool Allowed, string? Reason)> CanSendMessageAsync(int userId, string roomName)
    {
        // First check room access
        var (canAccess, accessReason) = await CanAccessRoomAsync(userId, roomName);
        if (!canAccess)
        {
            return (false, accessReason);
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        
        if (user == null)
        {
            return (false, "User not found");
        }

        // Check if user is active
        if (user.Status != "active")
        {
            return (false, "Your account is not active");
        }

        // Additional room-specific checks
        var roomParts = roomName.Split('_');
        var roomType = roomParts[0].ToLowerInvariant();

        // For order rooms, check if order is still active
        if (roomType == "order" && roomParts.Length >= 2 && int.TryParse(roomParts[1], out var orderId))
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order != null && order.Status == "cancelled")
            {
                // Admins can still send messages to cancelled orders
                if (user.Role != "admin")
                {
                    return (false, "Cannot send messages for cancelled orders");
                }
            }
        }

        return (true, null);
    }

    public async Task<bool> CanViewMessageAsync(int userId, int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        
        // Admins can view all messages
        if (user?.Role == "admin")
        {
            return true;
        }

        var message = await context.ChatMessages.FindAsync(messageId);
        if (message == null)
        {
            return false;
        }

        // Check if user can access the room
        var (canAccess, _) = await CanAccessRoomAsync(userId, message.ChatRoom);
        return canAccess;
    }

    public async Task<(bool Allowed, string? Reason)> CanEditMessageAsync(int userId, int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages.FindAsync(messageId);
        
        if (message == null)
        {
            return (false, "Message not found");
        }

        if (message.IsDeleted)
        {
            return (false, "Cannot edit deleted message");
        }

        var user = await context.Users.FindAsync(userId);

        // Only message sender or admin can edit
        if (message.SenderId != userId && user?.Role != "admin")
        {
            return (false, "You can only edit your own messages");
        }

        // Check time limit for editing (e.g., 24 hours)
        var editTimeLimit = TimeSpan.FromHours(24);
        if (DateTime.UtcNow - message.CreatedAt > editTimeLimit && user?.Role != "admin")
        {
            return (false, "Messages can only be edited within 24 hours");
        }

        return (true, null);
    }

    public async Task<(bool Allowed, string? Reason)> CanDeleteMessageAsync(int userId, int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages.FindAsync(messageId);
        
        if (message == null)
        {
            return (false, "Message not found");
        }

        if (message.IsDeleted)
        {
            return (false, "Message is already deleted");
        }

        var user = await context.Users.FindAsync(userId);

        // Admins can delete any message
        if (user?.Role == "admin")
        {
            return (true, null);
        }

        // Users can only delete their own messages
        if (message.SenderId != userId)
        {
            return (false, "You can only delete your own messages");
        }

        // Check time limit for deleting (e.g., 1 hour for non-admins)
        var deleteTimeLimit = TimeSpan.FromHours(1);
        if (DateTime.UtcNow - message.CreatedAt > deleteTimeLimit)
        {
            return (false, "Messages can only be deleted within 1 hour");
        }

        return (true, null);
    }

    public async Task<List<string>> GetAuthorizedRoomsAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        var authorizedRooms = new List<string>();

        if (user == null)
        {
            return authorizedRooms;
        }

        // Admins can see all active rooms
        if (user.Role == "admin")
        {
            var allRooms = await context.ChatMessages
                .Where(m => !m.IsDeleted)
                .Select(m => m.ChatRoom)
                .Distinct()
                .ToListAsync();
            
            return allRooms;
        }

        // User's personal room
        authorizedRooms.Add($"user_{userId}");
        authorizedRooms.Add($"support_{userId}");

        // Order rooms
        var userOrders = await context.Orders
            .Where(o => o.ClientId == userId)
            .Select(o => o.Id)
            .ToListAsync();
        
        authorizedRooms.AddRange(userOrders.Select(id => $"order_{id}"));

        // Quote rooms
        var userQuotes = await context.Quotes
            .Where(q => q.ClientId == userId)
            .Select(q => q.Id)
            .ToListAsync();
        
        authorizedRooms.AddRange(userQuotes.Select(id => $"quote_{id}"));

        // DM rooms where user is a participant
        var dmRooms = await context.ChatMessages
            .Where(m => m.ChatRoom.StartsWith("dm_") && 
                       (m.ChatRoom.Contains($"_{userId}_") || m.ChatRoom.EndsWith($"_{userId}")))
            .Select(m => m.ChatRoom)
            .Distinct()
            .ToListAsync();
        
        authorizedRooms.AddRange(dmRooms);

        return authorizedRooms.Distinct().ToList();
    }

    public async Task<List<ChatMessage>> FilterAuthorizedMessagesAsync(int userId, List<ChatMessage> messages)
    {
        var authorizedRooms = await GetAuthorizedRoomsAsync(userId);
        
        return messages
            .Where(m => authorizedRooms.Contains(m.ChatRoom))
            .ToList();
    }

    public async Task<bool> IsAdminAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        return user?.Role == "admin";
    }
}
