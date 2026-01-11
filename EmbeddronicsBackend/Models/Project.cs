namespace EmbeddronicsBackend.Models
{
    public class Project
    {
        public int Id { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("image")]
        public string ImageUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("tags")]
        public string[] Technologies { get; set; } = Array.Empty<string>();

        [System.Text.Json.Serialization.JsonPropertyName("client")]
        public string ClientName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("challenge")]
        public string Challenge { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("solution")]
        public string Solution { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("detail")]
        public string Detail { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("models")]
        public List<Model3D> Models { get; set; } = new();

        public DateTime CompletedDate { get; set; }
        public string Status { get; set; } = "Completed";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Model3D
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("fileType")]
        public string FileType { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "public";

        [System.Text.Json.Serialization.JsonPropertyName("linkedId")]
        public string LinkedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("uploadDate")]
        public string UploadDate { get; set; } = string.Empty;
    }
}
