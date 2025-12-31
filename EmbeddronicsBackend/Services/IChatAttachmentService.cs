using EmbeddronicsBackend.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for chat attachment file operations
/// </summary>
public interface IChatAttachmentService
{
    /// <summary>
    /// Upload a file attachment for a chat message
    /// </summary>
    Task<FileUploadResponseDto> UploadAttachmentAsync(int userId, IFormFile file, string chatRoom, int? messageId = null);

    /// <summary>
    /// Upload multiple file attachments
    /// </summary>
    Task<List<FileUploadResponseDto>> UploadAttachmentsAsync(int userId, IEnumerable<IFormFile> files, string chatRoom, int? messageId = null);

    /// <summary>
    /// Get attachment by ID
    /// </summary>
    Task<ChatAttachmentDto?> GetAttachmentAsync(int attachmentId);

    /// <summary>
    /// Get file stream for download
    /// </summary>
    Task<(Stream? FileStream, string FileName, string ContentType)?> GetAttachmentFileAsync(int attachmentId, int userId);

    /// <summary>
    /// Delete an attachment
    /// </summary>
    Task<bool> DeleteAttachmentAsync(int attachmentId, int userId);

    /// <summary>
    /// Link attachments to a message
    /// </summary>
    Task LinkAttachmentsToMessageAsync(int messageId, List<int> attachmentIds);

    /// <summary>
    /// Get attachments for a message
    /// </summary>
    Task<List<ChatAttachmentDto>> GetMessageAttachmentsAsync(int messageId);

    /// <summary>
    /// Validate file type and size
    /// </summary>
    (bool IsValid, string? Error) ValidateFile(IFormFile file);
}
