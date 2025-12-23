using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

public class Quote
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    public int? OrderId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected", "expired"

    public DateTime ValidUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ClientId")]
    public virtual User Client { get; set; } = null!;

    [ForeignKey("OrderId")]
    public virtual Order? Order { get; set; }

    public virtual ICollection<QuoteItem> Items { get; set; } = new List<QuoteItem>();
}