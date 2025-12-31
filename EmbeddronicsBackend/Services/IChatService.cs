using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for chat service operations
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Save a new chat message to the database
    /// </summary>
    Task<ChatMessageDto> SaveMessageAsync(int senderId, SendMessageDto messageDto);

    /// <summary>
    /// Edit an existing message
    /// </summary>
    Task<ChatMessageDto?> EditMessageAsync(int userId, EditMessageDto editDto);

    /// <summary>
    /// Delete a message (soft delete)
    /// </summary>
    Task<(bool Success, string Message, string? ChatRoom)> DeleteMessageAsync(int userId, int messageId);

    /// <summary>
    /// Mark messages as read
    /// </summary>
    Task MarkMessagesAsReadAsync(int userId, MarkMessagesReadDto dto);

    /// <summary>
    /// Get chat history for a room
    /// </summary>
    Task<ChatHistoryResponseDto> GetChatHistoryAsync(int userId, ChatHistoryRequestDto request);

    /// <summary>
    /// Get unread message count for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(int userId, string? chatRoom = null);

    /// <summary>
    /// Check if a user can access a specific chat room
    /// </summary>
    Task<bool> CanAccessChatRoomAsync(int userId, string chatRoom);

    /// <summary>
    /// Get available chat rooms for a user
    /// </summary>
    Task<List<ChatRoomDto>> GetUserChatRoomsAsync(int userId);

    /// <summary>
    /// Get a message by ID
    /// </summary>
    Task<ChatMessageDto?> GetMessageByIdAsync(int messageId);

    /// <summary>
    /// Search messages in a chat room
    /// </summary>
    Task<List<ChatMessageDto>> SearchMessagesAsync(int userId, string chatRoom, string searchTerm, int limit = 50);

    #region Threading & Conversation Management

    /// <summary>
    /// Reply to a message (creates a thread)
    /// </summary>
    Task<ChatMessageDto> ReplyToMessageAsync(int senderId, int parentMessageId, SendMessageDto messageDto);

    /// <summary>
    /// Get thread/conversation details including all replies
    /// </summary>
    Task<ThreadDetailsDto?> GetThreadAsync(int userId, int rootMessageId);

    /// <summary>
    /// Get replies to a specific message
    /// </summary>
    Task<List<ChatMessageDto>> GetRepliesAsync(int userId, int parentMessageId, int page = 1, int pageSize = 50);

    /// <summary>
    /// Create a new conversation/thread
    /// </summary>
    Task<ChatMessageDto> CreateConversationAsync(int userId, CreateConversationDto dto);

    /// <summary>
    /// Pin or unpin a message
    /// </summary>
    Task<bool> PinMessageAsync(int userId, PinMessageDto dto);

    /// <summary>
    /// Get pinned messages in a chat room
    /// </summary>
    Task<List<ChatMessageDto>> GetPinnedMessagesAsync(int userId, string chatRoom);

    #endregion

    #region Read Receipts

    /// <summary>
    /// Get read receipts for a message
    /// </summary>
    Task<List<MessageReadReceiptDto>> GetReadReceiptsAsync(int messageId);

    /// <summary>
    /// Mark a single message as read with receipt tracking
    /// </summary>
    Task<bool> MarkMessageAsReadWithReceiptAsync(int userId, int messageId);

    #endregion
}
