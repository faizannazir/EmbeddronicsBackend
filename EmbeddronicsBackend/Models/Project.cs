namespace EmbeddronicsBackend.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string[] Technologies { get; set; } = Array.Empty<string>();
        public string ClientName { get; set; } = string.Empty;
        public DateTime CompletedDate { get; set; }
        public string Status { get; set; } = "Completed";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
