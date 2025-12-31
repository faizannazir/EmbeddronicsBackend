using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

/// <summary>
/// Tracks SignalR connection information for users.
/// Supports multiple connections per user (e.g., multiple browser tabs/devices).
/// </summary>
public class UserConnection
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The user associated with this connection
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// SignalR connection ID
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// User agent string from the client
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// IP address of the connection
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Whether the connection is currently active
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Timestamp when the connection was established
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the connection was disconnected (null if still connected)
    /// </summary>
    public DateTime? DisconnectedAt { get; set; }

    /// <summary>
    /// Last activity timestamp for presence tracking
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
