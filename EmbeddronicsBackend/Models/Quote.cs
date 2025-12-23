namespace EmbeddronicsBackend.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public List<QuoteItem> Items { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? SentDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class QuoteItem
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
    }
}
