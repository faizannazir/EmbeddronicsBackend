using System.ComponentModel.DataAnnotations;

namespace EmbeddronicsBackend.Models.DTOs
{
    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? ImageUrl { get; set; }
        public List<string>? Features { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateProductRequest
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? ImageUrl { get; set; }
        public List<string>? Features { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? ImageUrl { get; set; }
        public List<string>? Features { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}