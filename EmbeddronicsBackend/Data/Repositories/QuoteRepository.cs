using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class QuoteRepository : Repository<Quote>, IQuoteRepository
{
    public QuoteRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Quote>> GetQuotesByClientIdAsync(int clientId)
    {
        return await _dbSet
            .Where(q => q.ClientId == clientId)
            .Include(q => q.Order)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Quote>> GetQuotesByOrderIdAsync(int orderId)
    {
        return await _dbSet
            .Where(q => q.OrderId == orderId)
            .Include(q => q.Items)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Quote>> GetQuotesByStatusAsync(string status)
    {
        return await _dbSet
            .Where(q => q.Status == status)
            .Include(q => q.Client)
            .Include(q => q.Order)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<Quote?> GetQuoteWithItemsAsync(int id)
    {
        return await _dbSet
            .Include(q => q.Client)
            .Include(q => q.Order)
            .Include(q => q.Items)
                .ThenInclude(qi => qi.Product)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<IEnumerable<Quote>> GetExpiredQuotesAsync()
    {
        return await _dbSet
            .Where(q => q.ValidUntil < DateTime.UtcNow && q.Status == "pending")
            .Include(q => q.Client)
            .ToListAsync();
    }

    public async Task<bool> UpdateQuoteStatusAsync(int id, string status)
    {
        var quote = await _dbSet.FindAsync(id);
        if (quote == null)
            return false;

        quote.Status = status;
        quote.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<IEnumerable<Quote>> GetQuotesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(q => q.CreatedAt >= startDate && q.CreatedAt <= endDate)
            .Include(q => q.Client)
            .Include(q => q.Order)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalQuoteValueByClientAsync(int clientId)
    {
        return await _dbSet
            .Where(q => q.ClientId == clientId && q.Status == "approved")
            .SumAsync(q => q.Amount);
    }

    public async Task<IEnumerable<Quote>> GetPendingQuotesAsync()
    {
        return await _dbSet
            .Where(q => q.Status == "pending" && q.ValidUntil > DateTime.UtcNow)
            .Include(q => q.Client)
            .Include(q => q.Order)
            .OrderBy(q => q.ValidUntil)
            .ToListAsync();
    }
}