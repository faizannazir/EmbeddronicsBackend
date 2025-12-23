namespace EmbeddronicsBackend.Models
{
    public class BlogPost
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
        public bool IsPublished { get; set; } = true;
        public int Views { get; set; } = 0;
    }
}
