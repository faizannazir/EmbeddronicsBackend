using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Services;
using System.Security.Claims;

namespace EmbeddronicsBackend.Controllers;

/// <summary>
/// Controller for chat-related HTTP endpoints.
/// Provides REST API for chat history, room management, and message retrieval.
/// Real-time messaging is handled by ChatHub via SignalR.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IConnectionManagerService _connectionManager;
    private readonly IChatAttachmentService _attachmentService;
    private readonly IUserStatusService _userStatusService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService, 
        IConnectionManagerService connectionManager,
        IChatAttachmentService attachmentService,
        IUserStatusService userStatusService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _connectionManager = connectionManager;
        _attachmentService = attachmentService;
        _userStatusService = userStatusService;
        _logger = logger;
    }

    /// <summary>
    /// Get chat rooms available to the current user
    /// </summary>
    [HttpGet("rooms")]
    public async Task<ActionResult<List<ChatRoomDto>>> GetChatRooms()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var rooms = await _chatService.GetUserChatRoomsAsync(userId.Value);
        return Ok(rooms);
    }

    /// <summary>
    /// Get chat history for a specific room
    /// </summary>
    [HttpGet("history/{chatRoom}")]
    public async Task<ActionResult<ChatHistoryResponseDto>> GetChatHistory(
        string chatRoom,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? before = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        // Validate access
        if (!await _chatService.CanAccessChatRoomAsync(userId.Value, chatRoom))
        {
            return Forbid("Access denied to this chat room");
        }

        var request = new ChatHistoryRequestDto
        {
            ChatRoom = chatRoom,
            Page = page,
            PageSize = Math.Min(pageSize, 100), // Limit max page size
            Before = before
        };

        var history = await _chatService.GetChatHistoryAsync(userId.Value, request);
        return Ok(history);
    }

    /// <summary>
    /// Get a specific message by ID
    /// </summary>
    [HttpGet("message/{messageId}")]
    public async Task<ActionResult<ChatMessageDto>> GetMessage(int messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var message = await _chatService.GetMessageByIdAsync(messageId);
        if (message == null)
            return NotFound();

        // Verify access
        if (!await _chatService.CanAccessChatRoomAsync(userId.Value, message.ChatRoom))
        {
            return Forbid();
        }

        return Ok(message);
    }

    /// <summary>
    /// Search messages in a chat room
    /// </summary>
    [HttpGet("search/{chatRoom}")]
    public async Task<ActionResult<List<ChatMessageDto>>> SearchMessages(
        string chatRoom,
        [FromQuery] string searchTerm,
        [FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(searchTerm))
            return BadRequest("Search term is required");

        var messages = await _chatService.SearchMessagesAsync(
            userId.Value, 
            chatRoom, 
            searchTerm, 
            Math.Min(limit, 100));

        return Ok(messages);
    }

    /// <summary>
    /// Get unread message count
    /// </summary>
    [HttpGet("unread")]
    public async Task<ActionResult<object>> GetUnreadCount([FromQuery] string? chatRoom = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var count = await _chatService.GetUnreadCountAsync(userId.Value, chatRoom);
        return Ok(new { unreadCount = count, chatRoom });
    }

    /// <summary>
    /// Mark messages as read
    /// </summary>
    [HttpPost("read")]
    public async Task<ActionResult> MarkAsRead([FromBody] MarkMessagesReadDto dto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        await _chatService.MarkMessagesAsReadAsync(userId.Value, dto);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Get online users (admin only)
    /// </summary>
    [HttpGet("online")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<UserPresenceDto>>> GetOnlineUsers()
    {
        var users = await _connectionManager.GetOnlineUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Get online users in a specific chat room
    /// </summary>
    [HttpGet("online/{chatRoom}")]
    public async Task<ActionResult<List<UserPresenceDto>>> GetOnlineUsersInRoom(string chatRoom)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        // Validate access
        if (!await _chatService.CanAccessChatRoomAsync(userId.Value, chatRoom))
        {
            return Forbid();
        }

        var users = await _connectionManager.GetOnlineUsersInRoomAsync(chatRoom);
        return Ok(users);
    }

    /// <summary>
    /// Get presence information for a specific user
    /// </summary>
    [HttpGet("presence/{targetUserId}")]
    public async Task<ActionResult<UserPresenceDto>> GetUserPresence(int targetUserId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var presence = await _connectionManager.GetUserPresenceAsync(targetUserId);
        if (presence == null)
            return NotFound();

        return Ok(presence);
    }

    /// <summary>
    /// Send a message via REST API (alternative to SignalR)
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] SendMessageDto messageDto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(messageDto.Content))
            return BadRequest("Message content is required");

        if (string.IsNullOrWhiteSpace(messageDto.ChatRoom))
            return BadRequest("Chat room is required");

        // Validate access
        if (!await _chatService.CanAccessChatRoomAsync(userId.Value, messageDto.ChatRoom))
        {
            return Forbid("Access denied to this chat room");
        }

        var message = await _chatService.SaveMessageAsync(userId.Value, messageDto);
        
        _logger.LogInformation("Message {MessageId} sent via REST API by user {UserId}", 
            message.Id, userId.Value);

        return CreatedAtAction(nameof(GetMessage), new { messageId = message.Id }, message);
    }

    /// <summary>
    /// Edit a message
    /// </summary>
    [HttpPut("message/{messageId}")]
    public async Task<ActionResult<ChatMessageDto>> EditMessage(int messageId, [FromBody] EditMessageDto editDto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        editDto.MessageId = messageId;
        var message = await _chatService.EditMessageAsync(userId.Value, editDto);
        
        if (message == null)
            return NotFound("Message not found or you don't have permission to edit it");

        return Ok(message);
    }

    /// <summary>
    /// Delete a message
    /// </summary>
    [HttpDelete("message/{messageId}")]
    public async Task<ActionResult> DeleteMessage(int messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await _chatService.DeleteMessageAsync(userId.Value, messageId);
        
        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(new { success = true, message = result.Message });
    }

    #region Threading & Conversation Endpoints

    /// <summary>
    /// Reply to a message (create a thread)
    /// </summary>
    [HttpPost("reply/{parentMessageId}")]
    public async Task<ActionResult<ChatMessageDto>> ReplyToMessage(int parentMessageId, [FromBody] SendMessageDto messageDto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(messageDto.Content))
            return BadRequest("Message content is required");

        try
        {
            var message = await _chatService.ReplyToMessageAsync(userId.Value, parentMessageId, messageDto);
            return CreatedAtAction(nameof(GetMessage), new { messageId = message.Id }, message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Get thread details including all replies
    /// </summary>
    [HttpGet("thread/{rootMessageId}")]
    public async Task<ActionResult<ThreadDetailsDto>> GetThread(int rootMessageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var thread = await _chatService.GetThreadAsync(userId.Value, rootMessageId);
        if (thread == null)
            return NotFound();

        return Ok(thread);
    }

    /// <summary>
    /// Get replies to a specific message
    /// </summary>
    [HttpGet("replies/{parentMessageId}")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetReplies(
        int parentMessageId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var replies = await _chatService.GetRepliesAsync(userId.Value, parentMessageId, page, pageSize);
        return Ok(replies);
    }

    /// <summary>
    /// Create a new conversation
    /// </summary>
    [HttpPost("conversation")]
    public async Task<ActionResult<ChatMessageDto>> CreateConversation([FromBody] CreateConversationDto dto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Subject))
            return BadRequest("Subject is required");

        if (string.IsNullOrWhiteSpace(dto.InitialMessage))
            return BadRequest("Initial message is required");

        try
        {
            var message = await _chatService.CreateConversationAsync(userId.Value, dto);
            return CreatedAtAction(nameof(GetThread), new { rootMessageId = message.Id }, message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Pin or unpin a message
    /// </summary>
    [HttpPost("pin")]
    public async Task<ActionResult> PinMessage([FromBody] PinMessageDto dto)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var success = await _chatService.PinMessageAsync(userId.Value, dto);
        if (!success)
            return BadRequest("Unable to pin/unpin message");

        return Ok(new { success = true, isPinned = dto.IsPinned });
    }

    /// <summary>
    /// Get pinned messages in a chat room
    /// </summary>
    [HttpGet("pinned/{chatRoom}")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetPinnedMessages(string chatRoom)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var messages = await _chatService.GetPinnedMessagesAsync(userId.Value, chatRoom);
        return Ok(messages);
    }

    /// <summary>
    /// Get read receipts for a message
    /// </summary>
    [HttpGet("receipts/{messageId}")]
    public async Task<ActionResult<List<MessageReadReceiptDto>>> GetReadReceipts(int messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var receipts = await _chatService.GetReadReceiptsAsync(messageId);
        return Ok(receipts);
    }

    #endregion

    #region File Attachment Endpoints

    /// <summary>
    /// Upload a file attachment
    /// </summary>
    [HttpPost("attachment/upload")]
    [RequestSizeLimit(52428800)] // 50MB limit
    public async Task<ActionResult<FileUploadResponseDto>> UploadAttachment(
        [FromForm] IFormFile file,
        [FromForm] string chatRoom,
        [FromForm] int? messageId = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        // Validate access
        if (!await _chatService.CanAccessChatRoomAsync(userId.Value, chatRoom))
        {
            return Forbid();
        }

        // Validate file
        var validation = _attachmentService.ValidateFile(file);
        if (!validation.IsValid)
        {
            return BadRequest(validation.Error);
        }

        try
        {
            var result = await _attachmentService.UploadAttachmentAsync(userId.Value, file, chatRoom, messageId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Upload multiple file attachments
    /// </summary>
    [HttpPost("attachment/upload-multiple")]
    [RequestSizeLimit(157286400)] // 150MB total limit for multiple files
    public async Task<ActionResult<List<FileUploadResponseDto>>> UploadMultipleAttachments(
        [FromForm] List<IFormFile> files,
        [FromForm] string chatRoom,
        [FromForm] int? messageId = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        // Validate access
        if (!await _chatService.CanAccessChatRoomAsync(userId.Value, chatRoom))
        {
            return Forbid();
        }

        // Validate all files
        foreach (var file in files)
        {
            var validation = _attachmentService.ValidateFile(file);
            if (!validation.IsValid)
            {
                return BadRequest($"Invalid file '{file.FileName}': {validation.Error}");
            }
        }

        try
        {
            var results = await _attachmentService.UploadAttachmentsAsync(userId.Value, files, chatRoom, messageId);
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Download an attachment
    /// </summary>
    [HttpGet("attachment/{attachmentId}/download")]
    public async Task<ActionResult> DownloadAttachment(int attachmentId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await _attachmentService.GetAttachmentFileAsync(attachmentId, userId.Value);
        if (result == null)
            return NotFound();

        return File(result.Value.FileStream!, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Get attachment metadata
    /// </summary>
    [HttpGet("attachment/{attachmentId}")]
    public async Task<ActionResult<ChatAttachmentDto>> GetAttachment(int attachmentId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var attachment = await _attachmentService.GetAttachmentAsync(attachmentId);
        if (attachment == null)
            return NotFound();

        return Ok(attachment);
    }

    /// <summary>
    /// Delete an attachment
    /// </summary>
    [HttpDelete("attachment/{attachmentId}")]
    public async Task<ActionResult> DeleteAttachment(int attachmentId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var success = await _attachmentService.DeleteAttachmentAsync(attachmentId, userId.Value);
        if (!success)
            return BadRequest("Unable to delete attachment");

        return Ok(new { success = true });
    }

    /// <summary>
    /// Get attachments for a message
    /// </summary>
    [HttpGet("message/{messageId}/attachments")]
    public async Task<ActionResult<List<ChatAttachmentDto>>> GetMessageAttachments(int messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var attachments = await _attachmentService.GetMessageAttachmentsAsync(messageId);
        return Ok(attachments);
    }

    #endregion

    #region Admin User Status Endpoints

    /// <summary>
    /// Get all users status (admin only)
    /// </summary>
    [HttpGet("admin/users/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<AdminUserStatusDto>>> GetAllUsersStatus()
    {
        var users = await _userStatusService.GetAllUsersStatusAsync();
        return Ok(users);
    }

    /// <summary>
    /// Get detailed status for a specific user (admin only)
    /// </summary>
    [HttpGet("admin/users/{targetUserId}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AdminUserStatusDto>> GetUserDetailedStatus(int targetUserId)
    {
        var status = await _userStatusService.GetUserStatusAsync(targetUserId);
        if (status == null)
            return NotFound();

        return Ok(status);
    }

    /// <summary>
    /// Get users with unread messages (admin only)
    /// </summary>
    [HttpGet("admin/users/unread")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<AdminUserStatusDto>>> GetUsersWithUnreadMessages()
    {
        var users = await _userStatusService.GetUsersWithUnreadMessagesAsync();
        return Ok(users);
    }

    /// <summary>
    /// Get recent activities for a user (admin only)
    /// </summary>
    [HttpGet("admin/users/{targetUserId}/activities")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<RecentActivityDto>>> GetUserActivities(int targetUserId, [FromQuery] int limit = 20)
    {
        var activities = await _userStatusService.GetRecentActivitiesAsync(targetUserId, limit);
        return Ok(activities);
    }

    /// <summary>
    /// Get read status summary for a user (admin only)
    /// </summary>
    [HttpGet("admin/users/{targetUserId}/read-status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<Dictionary<string, int>>> GetUserReadStatusSummary(int targetUserId)
    {
        var summary = await _userStatusService.GetReadStatusSummaryAsync(targetUserId);
        return Ok(summary);
    }

    /// <summary>
    /// Get conversation statistics (admin only)
    /// </summary>
    [HttpGet("admin/stats")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ConversationStatsDto>> GetConversationStats()
    {
        var stats = await _userStatusService.GetConversationStatsAsync();
        return Ok(stats);
    }

    #endregion

    #region Helper Methods

    private int? GetUserId()
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
