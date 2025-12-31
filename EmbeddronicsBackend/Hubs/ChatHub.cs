using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Services;
using System.Security.Claims;
using Serilog;

namespace EmbeddronicsBackend.Hubs;

/// <summary>
/// SignalR Hub for real-time chat functionality.
/// Handles message broadcasting, user presence, and room management.
/// Includes authorization, rate limiting, and session management.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IConnectionManagerService _connectionManager;
    private readonly IChatAuthorizationService _authorizationService;
    private readonly IChatRateLimitService _rateLimitService;
    private readonly ISessionManagementService _sessionManagement;

    public ChatHub(
        IChatService chatService, 
        IConnectionManagerService connectionManager,
        IChatAuthorizationService authorizationService,
        IChatRateLimitService rateLimitService,
        ISessionManagementService sessionManagement)
    {
        _chatService = chatService;
        _connectionManager = connectionManager;
        _authorizationService = authorizationService;
        _rateLimitService = rateLimitService;
        _sessionManagement = sessionManagement;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var userName = GetUserName();
        var userRole = GetUserRole();

        if (userId.HasValue)
        {
            // Check if user is blocked from chat
            var (isBlocked, blockReason, blockedUntil) = await _sessionManagement.IsUserBlockedAsync(userId.Value);
            if (isBlocked)
            {
                Log.Warning("Blocked user {UserId} attempted to connect. Reason: {Reason}", userId, blockReason);
                await Clients.Caller.SendAsync("ConnectionDenied", new
                {
                    Reason = blockReason,
                    BlockedUntil = blockedUntil,
                    Message = "Your access to chat has been suspended."
                });
                Context.Abort();
                return;
            }

            // Verify user can connect (active account, verified email, etc.)
            var (canConnect, connectReason) = await _authorizationService.CanConnectAsync(userId.Value);
            if (!canConnect)
            {
                Log.Warning("User {UserId} connection denied: {Reason}", userId, connectReason);
                await Clients.Caller.SendAsync("ConnectionDenied", new
                {
                    Reason = connectReason,
                    Message = "Unable to connect to chat."
                });
                Context.Abort();
                return;
            }

            var httpContext = Context.GetHttpContext();
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();

            // Register session metadata
            SessionManagementService.RegisterSession(Context.ConnectionId, ipAddress, userAgent);

            // Register the connection
            await _connectionManager.AddConnectionAsync(
                userId.Value, 
                Context.ConnectionId,
                userAgent,
                ipAddress
            );

            // Add user to their personal group for direct messages
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            SessionManagementService.AddRoomToSession(Context.ConnectionId, $"user_{userId}");

            // Add admin users to admin group
            if (userRole == "admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
                SessionManagementService.AddRoomToSession(Context.ConnectionId, "admins");
            }

            // Notify other users about presence
            var presence = new UserPresenceDto
            {
                UserId = userId.Value,
                UserName = userName ?? "Unknown",
                Role = userRole ?? "client",
                IsOnline = true,
                LastSeenAt = DateTime.UtcNow
            };

            await Clients.Others.SendAsync("UserConnected", presence);

            Log.Information("User {UserId} ({UserName}) connected with connection {ConnectionId} from {IpAddress}", 
                userId, userName, Context.ConnectionId, ipAddress);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var userName = GetUserName();

        if (userId.HasValue)
        {
            // Remove session metadata
            SessionManagementService.RemoveSession(Context.ConnectionId);

            // Remove the connection
            await _connectionManager.RemoveConnectionAsync(userId.Value, Context.ConnectionId);

            // Check if user has any remaining connections
            var isStillOnline = await _connectionManager.IsUserOnlineAsync(userId.Value);

            if (!isStillOnline)
            {
                // Notify other users about presence change
                var presence = new UserPresenceDto
                {
                    UserId = userId.Value,
                    UserName = userName ?? "Unknown",
                    IsOnline = false,
                    LastSeenAt = DateTime.UtcNow
                };

                await Clients.Others.SendAsync("UserDisconnected", presence);
            }

            Log.Information("User {UserId} ({UserName}) disconnected from connection {ConnectionId}. Exception: {Exception}", 
                userId, userName, Context.ConnectionId, exception?.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific chat room
    /// </summary>
    public async Task JoinRoom(string chatRoom)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        // Validate room access with enhanced authorization
        var (canAccess, accessReason) = await _authorizationService.CanAccessRoomAsync(userId.Value, chatRoom);
        if (!canAccess)
        {
            Log.Warning("User {UserId} denied access to room {ChatRoom}: {Reason}", userId, chatRoom, accessReason);
            throw new HubException(accessReason ?? "Access denied to this chat room");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, chatRoom);
        SessionManagementService.AddRoomToSession(Context.ConnectionId, chatRoom);
        await _connectionManager.UpdateUserActivityAsync(userId.Value, chatRoom);

        // Notify room members
        await Clients.Group(chatRoom).SendAsync("UserJoinedRoom", new
        {
            UserId = userId.Value,
            UserName = GetUserName(),
            ChatRoom = chatRoom,
            JoinedAt = DateTime.UtcNow
        });

        Log.Information("User {UserId} joined room {ChatRoom}", userId, chatRoom);
    }

    /// <summary>
    /// Leave a specific chat room
    /// </summary>
    public async Task LeaveRoom(string chatRoom)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatRoom);
        SessionManagementService.RemoveRoomFromSession(Context.ConnectionId, chatRoom);

        // Notify room members
        await Clients.Group(chatRoom).SendAsync("UserLeftRoom", new
        {
            UserId = userId.Value,
            UserName = GetUserName(),
            ChatRoom = chatRoom,
            LeftAt = DateTime.UtcNow
        });

        Log.Information("User {UserId} left room {ChatRoom}", userId, chatRoom);
    }

    /// <summary>
    /// Send a message to a chat room
    /// </summary>
    public async Task SendMessage(SendMessageDto messageDto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        // Check rate limit first
        var (rateLimitAllowed, rateLimitReason, retryAfter) = await _rateLimitService.CanSendMessageAsync(userId.Value);
        if (!rateLimitAllowed)
        {
            Log.Warning("User {UserId} rate limited in room {ChatRoom}: {Reason}", 
                userId, messageDto.ChatRoom, rateLimitReason);
            
            await Clients.Caller.SendAsync("RateLimited", new
            {
                Reason = rateLimitReason,
                RetryAfterSeconds = retryAfter,
                Message = "You are sending messages too fast. Please slow down."
            });
            
            throw new HubException(rateLimitReason ?? "Rate limit exceeded");
        }

        // Validate room access with enhanced authorization
        var (canSend, sendReason) = await _authorizationService.CanSendMessageAsync(userId.Value, messageDto.ChatRoom);
        if (!canSend)
        {
            Log.Warning("User {UserId} cannot send to room {ChatRoom}: {Reason}", 
                userId, messageDto.ChatRoom, sendReason);
            throw new HubException(sendReason ?? "Cannot send message to this chat room");
        }

        // Record message sent for rate limiting
        await _rateLimitService.RecordMessageSentAsync(userId.Value);

        // Update session activity
        SessionManagementService.UpdateSessionActivity(Context.ConnectionId);

        // Save message to database
        var savedMessage = await _chatService.SaveMessageAsync(userId.Value, messageDto);

        // Broadcast to room members
        await Clients.Group(messageDto.ChatRoom).SendAsync("ReceiveMessage", savedMessage);

        // If it's a direct message, also send to recipient's personal group
        if (messageDto.RecipientId.HasValue)
        {
            await Clients.Group($"user_{messageDto.RecipientId}").SendAsync("ReceiveDirectMessage", savedMessage);
        }

        // Notify admins for support messages
        if (messageDto.ChatRoom.StartsWith("support_") || messageDto.ChatRoom == "support")
        {
            await Clients.Group("admins").SendAsync("NewSupportMessage", savedMessage);
        }

        Log.Information("User {UserId} sent message to room {ChatRoom}: {MessageId}", 
            userId, messageDto.ChatRoom, savedMessage.Id);
    }

    /// <summary>
    /// Edit an existing message
    /// </summary>
    public async Task EditMessage(EditMessageDto editDto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        // Check authorization for editing
        var (canEdit, editReason) = await _authorizationService.CanEditMessageAsync(userId.Value, editDto.MessageId);
        if (!canEdit)
        {
            throw new HubException(editReason ?? "Cannot edit this message");
        }

        var editedMessage = await _chatService.EditMessageAsync(userId.Value, editDto);
        if (editedMessage == null)
        {
            throw new HubException("Failed to edit message. Message not found or unauthorized.");
        }

        // Broadcast the edit to room members
        await Clients.Group(editedMessage.ChatRoom).SendAsync("MessageEdited", editedMessage);

        Log.Information("User {UserId} edited message {MessageId}", userId, editDto.MessageId);
    }

    /// <summary>
    /// Delete a message (soft delete)
    /// </summary>
    public async Task DeleteMessage(int messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        // Check authorization for deleting
        var (canDelete, deleteReason) = await _authorizationService.CanDeleteMessageAsync(userId.Value, messageId);
        if (!canDelete)
        {
            throw new HubException(deleteReason ?? "Cannot delete this message");
        }

        var result = await _chatService.DeleteMessageAsync(userId.Value, messageId);
        if (!result.Success)
        {
            throw new HubException(result.Message);
        }

        // Broadcast the deletion to room members
        await Clients.Group(result.ChatRoom!).SendAsync("MessageDeleted", new
        {
            MessageId = messageId,
            DeletedBy = userId.Value,
            DeletedAt = DateTime.UtcNow
        });

        Log.Information("User {UserId} deleted message {MessageId}", userId, messageId);
    }

    /// <summary>
    /// Mark messages as read
    /// </summary>
    public async Task MarkMessagesAsRead(MarkMessagesReadDto dto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        await _chatService.MarkMessagesAsReadAsync(userId.Value, dto);

        // Notify senders that their messages were read
        await Clients.Group(dto.ChatRoom ?? "").SendAsync("MessagesRead", new
        {
            ReadBy = userId.Value,
            MessageIds = dto.MessageIds,
            ReadAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send typing indicator
    /// </summary>
    public async Task SendTypingIndicator(string chatRoom, bool isTyping)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var indicator = new TypingIndicatorDto
        {
            UserId = userId.Value,
            UserName = GetUserName() ?? "Unknown",
            ChatRoom = chatRoom,
            IsTyping = isTyping
        };

        await Clients.OthersInGroup(chatRoom).SendAsync("TypingIndicator", indicator);
    }

    /// <summary>
    /// Get online users in a chat room
    /// </summary>
    public async Task<List<UserPresenceDto>> GetOnlineUsers(string chatRoom)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        return await _connectionManager.GetOnlineUsersInRoomAsync(chatRoom);
    }

    /// <summary>
    /// Get unread message count
    /// </summary>
    public async Task<int> GetUnreadCount(string? chatRoom = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return 0;

        return await _chatService.GetUnreadCountAsync(userId.Value, chatRoom);
    }

    #region Helper Methods

    private int? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                       ?? Context.User?.FindFirst("userId")?.Value;
        
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    private string? GetUserName()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value 
            ?? Context.User?.FindFirst("name")?.Value;
    }

    private string? GetUserRole()
    {
        return Context.User?.FindFirst(ClaimTypes.Role)?.Value 
            ?? Context.User?.FindFirst("role")?.Value;
    }

    #endregion
}
