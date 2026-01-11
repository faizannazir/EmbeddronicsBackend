namespace EmbeddronicsBackend.Models
{
    public class Product
    {
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("price")]
        public decimal Price { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("image")]
        public string ImageUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("features")]
        public string[] Features { get; set; } = Array.Empty<string>();

        [System.Text.Json.Serialization.JsonPropertyName("models")]
        public List<Model3D> Models { get; set; } = new();

        public bool InStock { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
