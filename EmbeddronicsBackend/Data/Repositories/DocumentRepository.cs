using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Document>> GetDocumentsByOrderIdAsync(int orderId)
    {
        return await _dbSet
            .Where(d => d.OrderId == orderId)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Document>> GetDocumentsByFileTypeAsync(string fileType)
    {
        return await _dbSet
            .Where(d => d.FileType == fileType)
            .Include(d => d.Order)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<Document?> GetDocumentByFilePathAsync(string filePath)
    {
        return await _dbSet
            .Include(d => d.Order)
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(d => d.FilePath == filePath);
    }

    public async Task<IEnumerable<Document>> GetDocumentsByUploaderAsync(int uploaderId)
    {
        return await _dbSet
            .Where(d => d.UploadedById == uploaderId)
            .Include(d => d.Order)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<long> GetTotalFileSizeByOrderAsync(int orderId)
    {
        return await _dbSet
            .Where(d => d.OrderId == orderId)
            .SumAsync(d => d.FileSize);
    }

    public async Task<bool> DocumentExistsAsync(string fileName, int orderId)
    {
        return await _dbSet
            .AnyAsync(d => d.FileName == fileName && d.OrderId == orderId);
    }

    public async Task<IEnumerable<Document>> GetRecentDocumentsAsync(int count)
    {
        return await _dbSet
            .Include(d => d.Order)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}