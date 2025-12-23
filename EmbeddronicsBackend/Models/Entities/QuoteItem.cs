using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

public class QuoteItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int QuoteId { get; set; }

    public int? ProductId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int Quantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    // Navigation properties
    [ForeignKey("QuoteId")]
    public virtual Quote Quote { get; set; } = null!;

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }
}