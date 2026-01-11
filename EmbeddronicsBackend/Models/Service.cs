namespace EmbeddronicsBackend.Models
{
    public class Service
    {
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("iconName")]
        public string Icon { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("features")]
        public string[] Features { get; set; } = Array.Empty<string>();

        [System.Text.Json.Serialization.JsonPropertyName("image")]
        public string? Image { get; set; }

        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
