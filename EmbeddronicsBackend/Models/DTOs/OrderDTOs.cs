using System.ComponentModel.DataAnnotations;

namespace EmbeddronicsBackend.Models.DTOs
{
    public class CreateOrderRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PcbSpecsDto? PcbSpecs { get; set; }
        public string? BudgetRange { get; set; }
        public string? Timeline { get; set; }
    }

    public class UpdateOrderRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public PcbSpecsDto? PcbSpecs { get; set; }
        public string? BudgetRange { get; set; }
        public string? Timeline { get; set; }
        public string? Status { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PcbSpecsDto? PcbSpecs { get; set; }
        public string? BudgetRange { get; set; }
        public string? Timeline { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<MessageDto>? Messages { get; set; }
        public List<DocumentDto>? Documents { get; set; }
    }

    public class PcbSpecsDto
    {
        public string? BoardType { get; set; }
        public string? Layers { get; set; }
        public string? Dimensions { get; set; }
        public string? Material { get; set; }
        public string? Thickness { get; set; }
        public string? SolderMask { get; set; }
        public string? Silkscreen { get; set; }
        public string? Quantity { get; set; }
        public Dictionary<string, object>? AdditionalSpecs { get; set; }
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<AttachmentDto>? Attachments { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DocumentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class AttachmentDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class SendMessageRequest
    {
        public int OrderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<AttachmentDto>? Attachments { get; set; }
    }
}