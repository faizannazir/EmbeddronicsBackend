namespace EmbeddronicsBackend.Models
{
    public class FinancialRecord
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // Income, Expense
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public string Reference { get; set; } = string.Empty; // Order number, invoice, etc.
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
