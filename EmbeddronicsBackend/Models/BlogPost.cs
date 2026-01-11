namespace EmbeddronicsBackend.Models
{
    public class BlogPost
    {
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("excerpt")]
        public string Excerpt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("date")]
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;

        [System.Text.Json.Serialization.JsonPropertyName("image")]
        public string ImageUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "published";

        public string[] Tags { get; set; } = Array.Empty<string>();
        public bool IsPublished { get; set; } = true;
        public int Views { get; set; } = 0;
    }
}
