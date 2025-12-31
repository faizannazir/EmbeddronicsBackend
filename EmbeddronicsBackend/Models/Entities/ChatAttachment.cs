using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

/// <summary>
/// Represents a file attachment in the chat system.
/// Supports secure storage and retrieval of chat attachments.
/// </summary>
public class ChatAttachment
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The message this attachment belongs to
    /// </summary>
    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// The user who uploaded the attachment
    /// </summary>
    [Required]
    public int UploadedById { get; set; }

    /// <summary>
    /// Original filename
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Stored filename (GUID-based for security)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>
    /// File path relative to storage root
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the file
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// File extension (e.g., ".pdf", ".jpg")
    /// </summary>
    [MaxLength(20)]
    public string? FileExtension { get; set; }

    /// <summary>
    /// Thumbnail path for images/videos (if generated)
    /// </summary>
    [MaxLength(500)]
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// File category: "image", "document", "video", "audio", "other"
    /// </summary>
    [MaxLength(50)]
    public string FileCategory { get; set; } = "other";

    /// <summary>
    /// Whether the file has been scanned for viruses
    /// </summary>
    public bool IsScanned { get; set; } = false;

    /// <summary>
    /// Whether the file passed virus scan
    /// </summary>
    public bool IsSafe { get; set; } = true;

    /// <summary>
    /// Hash of the file for integrity checking
    /// </summary>
    [MaxLength(128)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Number of times the file has been downloaded
    /// </summary>
    public int DownloadCount { get; set; } = 0;

    /// <summary>
    /// Whether the attachment is deleted
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("MessageId")]
    public virtual ChatMessage Message { get; set; } = null!;

    [ForeignKey("UploadedById")]
    public virtual User UploadedBy { get; set; } = null!;
}
