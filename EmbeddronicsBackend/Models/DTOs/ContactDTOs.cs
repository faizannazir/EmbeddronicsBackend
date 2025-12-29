using System.ComponentModel.DataAnnotations;

namespace EmbeddronicsBackend.Models.DTOs
{
    public class ContactFormRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Phone { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ProjectType { get; set; }
        public string? BudgetRange { get; set; }
        public string? Timeline { get; set; }
    }

    public class ContactFormResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
    }
}