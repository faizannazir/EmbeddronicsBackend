using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

/// <summary>
/// Represents a chat message in the real-time chat system.
/// Messages can be tied to an order context or be direct messages between users.
/// Supports threading through ParentMessageId for conversation management.
/// </summary>
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The user who sent the message
    /// </summary>
    [Required]
    public int SenderId { get; set; }

    /// <summary>
    /// The user who receives the message (null for broadcast messages)
    /// </summary>
    public int? RecipientId { get; set; }

    /// <summary>
    /// Optional order context for the chat (if related to a specific order)
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>
    /// Parent message ID for threading support (null for root messages)
    /// </summary>
    public int? ParentMessageId { get; set; }

    /// <summary>
    /// Conversation/Thread ID to group related messages
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// Chat room/channel identifier (e.g., "order_123", "support", "admin")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ChatRoom { get; set; } = string.Empty;

    /// <summary>
    /// The message content
    /// </summary>
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of message: "text", "file", "system", "notification", "reply"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MessageType { get; set; } = "text";

    /// <summary>
    /// JSON string for file attachments metadata
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Attachments { get; set; }

    /// <summary>
    /// Priority level: "normal", "high", "urgent"
    /// </summary>
    [MaxLength(20)]
    public string Priority { get; set; } = "normal";

    /// <summary>
    /// Whether the message has been read by the recipient
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Timestamp when the message was read
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Whether the message has been edited
    /// </summary>
    public bool IsEdited { get; set; } = false;

    /// <summary>
    /// Whether the message has been deleted (soft delete)
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Whether the message is pinned in the conversation
    /// </summary>
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// Number of replies to this message (denormalized for performance)
    /// </summary>
    public int ReplyCount { get; set; } = 0;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("SenderId")]
    public virtual User Sender { get; set; } = null!;

    [ForeignKey("RecipientId")]
    public virtual User? Recipient { get; set; }

    [ForeignKey("OrderId")]
    public virtual Order? Order { get; set; }

    [ForeignKey("ParentMessageId")]
    public virtual ChatMessage? ParentMessage { get; set; }

    public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ChatAttachment> ChatAttachments { get; set; } = new List<ChatAttachment>();
}
