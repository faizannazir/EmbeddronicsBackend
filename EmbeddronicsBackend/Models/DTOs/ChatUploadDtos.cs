using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EmbeddronicsBackend.Models.DTOs
{
    /// <summary>
    /// DTO for single file upload via multipart/form-data
    /// </summary>
    public class UploadAttachmentRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        public string ChatRoom { get; set; } = string.Empty;

        public int? MessageId { get; set; }
    }

    /// <summary>
    /// DTO for multiple files upload via multipart/form-data
    /// </summary>
    public class UploadMultipleAttachmentsRequestDto
    {
        [Required]
        public List<IFormFile> Files { get; set; } = new();

        [Required]
        public string ChatRoom { get; set; } = string.Empty;

        public int? MessageId { get; set; }
    }
}
