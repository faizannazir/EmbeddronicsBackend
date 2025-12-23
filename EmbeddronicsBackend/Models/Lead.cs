namespace EmbeddronicsBackend.Models
{
    public class Lead
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // Website, Referral, etc.
        public string Status { get; set; } = "New"; // New, Contacted, Qualified, Converted, Lost
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastContactDate { get; set; }
    }
}
