using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

/// <summary>
/// Tracks user activity and last seen status for admin visibility.
/// Provides comprehensive user status information.
/// </summary>
public class UserActivityLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The user this activity belongs to
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Type of activity: "login", "logout", "message_sent", "message_read", "file_upload", "page_view"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Additional details about the activity (JSON)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ActivityDetails { get; set; }

    /// <summary>
    /// Chat room where activity occurred (if applicable)
    /// </summary>
    [MaxLength(100)]
    public string? ChatRoom { get; set; }

    /// <summary>
    /// IP address of the user
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp of the activity
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

/// <summary>
/// Tracks message read receipts for detailed read status
/// </summary>
public class MessageReadReceipt
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The message that was read
    /// </summary>
    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// The user who read the message
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Timestamp when the message was read
    /// </summary>
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("MessageId")]
    public virtual ChatMessage Message { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
