using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IQuoteRepository : IRepository<Quote>
{
    Task<IEnumerable<Quote>> GetQuotesByClientIdAsync(int clientId);
    Task<IEnumerable<Quote>> GetQuotesByOrderIdAsync(int orderId);
    Task<IEnumerable<Quote>> GetQuotesByStatusAsync(string status);
    Task<Quote?> GetQuoteWithItemsAsync(int id);
    Task<IEnumerable<Quote>> GetExpiredQuotesAsync();
    Task<bool> UpdateQuoteStatusAsync(int id, string status);
    Task<IEnumerable<Quote>> GetQuotesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalQuoteValueByClientAsync(int clientId);
    Task<IEnumerable<Quote>> GetPendingQuotesAsync();
}