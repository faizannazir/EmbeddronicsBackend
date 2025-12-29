using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> GetDocumentsByOrderIdAsync(int orderId);
    Task<IEnumerable<Document>> GetDocumentsByFileTypeAsync(string fileType);
    Task<Document?> GetDocumentByFilePathAsync(string filePath);
    Task<IEnumerable<Document>> GetDocumentsByUploaderAsync(int uploaderId);
    Task<long> GetTotalFileSizeByOrderAsync(int orderId);
    Task<bool> DocumentExistsAsync(string fileName, int orderId);
    Task<IEnumerable<Document>> GetRecentDocumentsAsync(int count);
}