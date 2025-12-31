using System.ComponentModel.DataAnnotations;

namespace EmbeddronicsBackend.Models.DTOs
{
    public class CreateQuoteRequest
    {
        public int ClientId { get; set; }
        public int? OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime ValidUntil { get; set; }
        public List<CreateQuoteItemRequest>? Items { get; set; }
    }

    public class UpdateQuoteRequest
    {
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Status { get; set; }
        public List<CreateQuoteItemRequest>? Items { get; set; }
    }

    public class QuoteDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public string? OrderTitle { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ValidUntil { get; set; }
        public List<QuoteItemDto>? Items { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateQuoteItemRequest
    {
        public int? ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class QuoteItemDto
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class QuoteAcceptanceRequest
    {
        public bool Accept { get; set; }
        public string? Notes { get; set; }
    }

    public class QuoteWorkflowStatusDto
    {
        public int QuoteId { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public string WorkflowStage { get; set; } = string.Empty;
        public bool CanModify { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public bool CanAccept { get; set; }
        public List<string> AvailableActions { get; set; } = new();
        public DateTime? LastStatusChange { get; set; }
        public string? LastActionBy { get; set; }
        public string? Notes { get; set; }
    }

    public class QuoteRevisionRequest
    {
        public int QuoteId { get; set; }
        public string RevisionNotes { get; set; } = string.Empty;
        public List<CreateQuoteItemRequest>? UpdatedItems { get; set; }
        public decimal? NewAmount { get; set; }
        public DateTime? NewValidUntil { get; set; }
    }

    public class QuoteApprovalRequest
    {
        public bool Approve { get; set; }
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
    }
}