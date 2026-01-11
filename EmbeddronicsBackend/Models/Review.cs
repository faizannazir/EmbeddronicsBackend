namespace EmbeddronicsBackend.Models
{
    public class Review
    {
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("clientName")]
        public string ClientName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("clientType")]
        public string ClientType { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("serviceProvided")]
        public string ServiceProvided { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Comment { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("rating")]
        public int Rating { get; set; } // 1-5

        [System.Text.Json.Serialization.JsonPropertyName("approved")]
        public bool IsApproved { get; set; } = false;

        [System.Text.Json.Serialization.JsonPropertyName("date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public string ClientEmail { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }
}
