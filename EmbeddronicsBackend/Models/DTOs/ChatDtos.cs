namespace EmbeddronicsBackend.Models.DTOs;

/// <summary>
/// DTO for sending a chat message via SignalR
/// </summary>
public class SendMessageDto
{
    public int? RecipientId { get; set; }
    public int? OrderId { get; set; }
    public int? ParentMessageId { get; set; }
    public int? ConversationId { get; set; }
    public string ChatRoom { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    public string Priority { get; set; } = "normal";
    public string? Attachments { get; set; }
    public List<int>? AttachmentIds { get; set; }
}

/// <summary>
/// DTO for receiving chat messages
/// </summary>
public class ChatMessageDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public int? RecipientId { get; set; }
    public string? RecipientName { get; set; }
    public int? OrderId { get; set; }
    public int? ParentMessageId { get; set; }
    public int? ConversationId { get; set; }
    public string ChatRoom { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    public string Priority { get; set; } = "normal";
    public string? Attachments { get; set; }
    public List<ChatAttachmentDto> FileAttachments { get; set; } = new();
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsEdited { get; set; }
    public bool IsPinned { get; set; }
    public int ReplyCount { get; set; }
    public ChatMessageDto? ParentMessage { get; set; }
    public List<MessageReadReceiptDto> ReadReceipts { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for chat attachments
/// </summary>
public class ChatAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileCategory { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for file upload request
/// </summary>
public class FileUploadDto
{
    public string ChatRoom { get; set; } = string.Empty;
    public int? MessageId { get; set; }
}

/// <summary>
/// DTO for file upload response
/// </summary>
public class FileUploadResponseDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileCategory { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
/// DTO for message read receipt
/// </summary>
public class MessageReadReceiptDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}

/// <summary>
/// DTO for editing a message
/// </summary>
public class EditMessageDto
{
    public int MessageId { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// DTO for marking messages as read
/// </summary>
public class MarkMessagesReadDto
{
    public List<int> MessageIds { get; set; } = new();
    public string? ChatRoom { get; set; }
}

/// <summary>
/// DTO for user presence information
/// </summary>
public class UserPresenceDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Company { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? CurrentChatRoom { get; set; }
    public int UnreadMessagesCount { get; set; }
    public string? LastActivity { get; set; }
}

/// <summary>
/// DTO for detailed admin user status view
/// </summary>
public class AdminUserStatusDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? CurrentChatRoom { get; set; }
    public int TotalMessagesSent { get; set; }
    public int TotalMessagesReceived { get; set; }
    public int UnreadMessagesCount { get; set; }
    public int ActiveConversations { get; set; }
    public List<RecentActivityDto> RecentActivities { get; set; } = new();
    public List<ConversationSummaryDto> OpenConversations { get; set; } = new();
}

/// <summary>
/// DTO for recent activity
/// </summary>
public class RecentActivityDto
{
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ChatRoom { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DTO for conversation summary
/// </summary>
public class ConversationSummaryDto
{
    public string ChatRoom { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
}

/// <summary>
/// DTO for typing indicator
/// </summary>
public class TypingIndicatorDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ChatRoom { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
}

/// <summary>
/// DTO for chat room information
/// </summary>
public class ChatRoomDto
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty; // "order", "support", "direct"
    public int? OrderId { get; set; }
    public List<UserPresenceDto> Participants { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalMessages { get; set; }
    public ChatMessageDto? LastMessage { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// DTO for paginated chat history request
/// </summary>
public class ChatHistoryRequestDto
{
    public string ChatRoom { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? Before { get; set; }
    public int? ConversationId { get; set; }
    public bool IncludeReplies { get; set; } = false;
}

/// <summary>
/// DTO for paginated chat history response
/// </summary>
public class ChatHistoryResponseDto
{
    public string ChatRoom { get; set; } = string.Empty;
    public List<ChatMessageDto> Messages { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// DTO for thread/conversation details
/// </summary>
public class ThreadDetailsDto
{
    public int RootMessageId { get; set; }
    public ChatMessageDto RootMessage { get; set; } = null!;
    public List<ChatMessageDto> Replies { get; set; } = new();
    public int TotalReplies { get; set; }
    public List<UserPresenceDto> Participants { get; set; } = new();
}

/// <summary>
/// DTO for conversation creation
/// </summary>
public class CreateConversationDto
{
    public string ChatRoom { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string InitialMessage { get; set; } = string.Empty;
    public List<int>? ParticipantIds { get; set; }
    public string Priority { get; set; } = "normal";
}

/// <summary>
/// DTO for pinning/unpinning a message
/// </summary>
public class PinMessageDto
{
    public int MessageId { get; set; }
    public bool IsPinned { get; set; }
}
