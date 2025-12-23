using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmbeddronicsBackend.Models.Entities;

public class Order
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "new"; // "new", "in_progress", "completed", "cancelled"

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? PcbSpecs { get; set; } // JSON string for PCB specifications

    [MaxLength(100)]
    public string? BudgetRange { get; set; }

    [MaxLength(100)]
    public string? Timeline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ClientId")]
    public virtual User Client { get; set; } = null!;
    
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public virtual ICollection<Quote> Quotes { get; set; } = new List<Quote>();
}