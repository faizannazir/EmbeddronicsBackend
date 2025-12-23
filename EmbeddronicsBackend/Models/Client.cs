namespace EmbeddronicsBackend.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime RegisteredDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Active"; // Active, Inactive, Suspended
        public int TotalOrders { get; set; } = 0;
        public decimal TotalSpent { get; set; } = 0;
    }
}
