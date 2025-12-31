using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Cryptography;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Service for handling chat file attachments with secure storage
/// </summary>
public class ChatAttachmentService : IChatAttachmentService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    // Allowed file extensions by category
    private static readonly Dictionary<string, string[]> AllowedExtensions = new()
    {
        { "image", new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" } },
        { "document", new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".csv" } },
        { "video", new[] { ".mp4", ".webm", ".avi", ".mov", ".wmv" } },
        { "audio", new[] { ".mp3", ".wav", ".ogg", ".m4a", ".flac" } },
        { "archive", new[] { ".zip", ".rar", ".7z", ".tar", ".gz" } },
        { "design", new[] { ".psd", ".ai", ".sketch", ".fig", ".xd" } },
        { "cad", new[] { ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs" } }
    };

    // Maximum file size (50MB default)
    private const long MaxFileSize = 52428800;

    public ChatAttachmentService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<FileUploadResponseDto> UploadAttachmentAsync(
        int userId, 
        IFormFile file, 
        string chatRoom, 
        int? messageId = null)
    {
        // Validate file
        var validation = ValidateFile(file);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Generate secure filename
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid()}{fileExtension}";
        var fileCategory = GetFileCategory(fileExtension);
        
        // Create directory structure: uploads/chat/{year}/{month}/{day}
        var datePath = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var relativePath = Path.Combine("uploads", "chat", datePath);
        var absolutePath = Path.Combine(_environment.ContentRootPath, relativePath);
        
        Directory.CreateDirectory(absolutePath);

        var fullFilePath = Path.Combine(absolutePath, storedFileName);
        var relativeFilePath = Path.Combine(relativePath, storedFileName);

        // Calculate file hash
        string fileHash;
        using (var sha256 = SHA256.Create())
        {
            using var stream = file.OpenReadStream();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            fileHash = Convert.ToHexString(hashBytes);
        }

        // Save file
        using (var stream = new FileStream(fullFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Generate thumbnail for images
        string? thumbnailPath = null;
        if (fileCategory == "image")
        {
            thumbnailPath = await GenerateThumbnailAsync(fullFilePath, absolutePath, storedFileName);
        }

        // Create database record
        var attachment = new ChatAttachment
        {
            MessageId = messageId ?? 0, // Will be updated when linked to message
            UploadedById = userId,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            FilePath = relativeFilePath.Replace("\\", "/"),
            ContentType = file.ContentType,
            FileSize = file.Length,
            FileExtension = fileExtension,
            ThumbnailPath = thumbnailPath?.Replace("\\", "/"),
            FileCategory = fileCategory,
            FileHash = fileHash,
            IsScanned = true, // TODO: Implement actual virus scanning
            IsSafe = true,
            CreatedAt = DateTime.UtcNow
        };

        context.ChatAttachments.Add(attachment);
        await context.SaveChangesAsync();

        Log.Information("Chat attachment {AttachmentId} uploaded by user {UserId} to room {ChatRoom}", 
            attachment.Id, userId, chatRoom);

        return new FileUploadResponseDto
        {
            AttachmentId = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            FileCategory = attachment.FileCategory,
            ThumbnailUrl = thumbnailPath != null ? $"/api/chat/attachment/{attachment.Id}/thumbnail" : null,
            DownloadUrl = $"/api/chat/attachment/{attachment.Id}/download"
        };
    }

    public async Task<List<FileUploadResponseDto>> UploadAttachmentsAsync(
        int userId, 
        IEnumerable<IFormFile> files, 
        string chatRoom, 
        int? messageId = null)
    {
        var results = new List<FileUploadResponseDto>();
        
        foreach (var file in files)
        {
            var result = await UploadAttachmentAsync(userId, file, chatRoom, messageId);
            results.Add(result);
        }

        return results;
    }

    public async Task<ChatAttachmentDto?> GetAttachmentAsync(int attachmentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var attachment = await context.ChatAttachments
            .Where(a => a.Id == attachmentId && !a.IsDeleted)
            .FirstOrDefaultAsync();

        if (attachment == null) return null;

        return new ChatAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            FileCategory = attachment.FileCategory,
            ThumbnailUrl = attachment.ThumbnailPath != null ? $"/api/chat/attachment/{attachment.Id}/thumbnail" : null,
            DownloadUrl = $"/api/chat/attachment/{attachment.Id}/download",
            CreatedAt = attachment.CreatedAt
        };
    }

    public async Task<(Stream? FileStream, string FileName, string ContentType)?> GetAttachmentFileAsync(
        int attachmentId, 
        int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var attachment = await context.ChatAttachments
            .Include(a => a.Message)
            .Where(a => a.Id == attachmentId && !a.IsDeleted)
            .FirstOrDefaultAsync();

        if (attachment == null) return null;

        // Verify user has access to the chat room
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        if (attachment.Message != null && !await chatService.CanAccessChatRoomAsync(userId, attachment.Message.ChatRoom))
        {
            return null;
        }

        var fullPath = Path.Combine(_environment.ContentRootPath, attachment.FilePath);
        if (!File.Exists(fullPath))
        {
            Log.Warning("Attachment file not found: {FilePath}", fullPath);
            return null;
        }

        // Update download count
        attachment.DownloadCount++;
        await context.SaveChangesAsync();

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, attachment.FileName, attachment.ContentType);
    }

    public async Task<bool> DeleteAttachmentAsync(int attachmentId, int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var attachment = await context.ChatAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null) return false;

        // Only the uploader or admin can delete
        var user = await context.Users.FindAsync(userId);
        if (attachment.UploadedById != userId && user?.Role != "admin")
        {
            return false;
        }

        // Soft delete
        attachment.IsDeleted = true;
        await context.SaveChangesAsync();

        Log.Information("Chat attachment {AttachmentId} deleted by user {UserId}", attachmentId, userId);

        return true;
    }

    public async Task LinkAttachmentsToMessageAsync(int messageId, List<int> attachmentIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var attachments = await context.ChatAttachments
            .Where(a => attachmentIds.Contains(a.Id) && !a.IsDeleted)
            .ToListAsync();

        foreach (var attachment in attachments)
        {
            attachment.MessageId = messageId;
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<ChatAttachmentDto>> GetMessageAttachmentsAsync(int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        return await context.ChatAttachments
            .Where(a => a.MessageId == messageId && !a.IsDeleted)
            .Select(a => new ChatAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSize = a.FileSize,
                FileCategory = a.FileCategory,
                ThumbnailUrl = a.ThumbnailPath != null ? $"/api/chat/attachment/{a.Id}/thumbnail" : null,
                DownloadUrl = $"/api/chat/attachment/{a.Id}/download",
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public (bool IsValid, string? Error) ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "File is empty");
        }

        if (file.Length > MaxFileSize)
        {
            return (false, $"File size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allAllowedExtensions = AllowedExtensions.Values.SelectMany(e => e).ToHashSet();

        if (!allAllowedExtensions.Contains(extension))
        {
            return (false, $"File type '{extension}' is not allowed");
        }

        // Validate content type matches extension
        var expectedCategory = GetFileCategory(extension);
        if (!IsValidContentType(file.ContentType, expectedCategory))
        {
            return (false, "File content type does not match file extension");
        }

        return (true, null);
    }

    private static string GetFileCategory(string extension)
    {
        foreach (var category in AllowedExtensions)
        {
            if (category.Value.Contains(extension.ToLowerInvariant()))
            {
                return category.Key;
            }
        }
        return "other";
    }

    private static bool IsValidContentType(string contentType, string category)
    {
        return category switch
        {
            "image" => contentType.StartsWith("image/"),
            "document" => contentType.StartsWith("application/") || contentType == "text/plain" || contentType == "text/csv",
            "video" => contentType.StartsWith("video/"),
            "audio" => contentType.StartsWith("audio/"),
            "archive" => contentType.Contains("zip") || contentType.Contains("compressed") || contentType.Contains("archive"),
            _ => true
        };
    }

    private async Task<string?> GenerateThumbnailAsync(string sourcePath, string targetDirectory, string fileName)
    {
        try
        {
            // For now, we'll skip actual thumbnail generation
            // In production, use ImageSharp or SkiaSharp for thumbnail generation
            // This is a placeholder that returns null
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to generate thumbnail for {FileName}", fileName);
            return null;
        }
    }
}
