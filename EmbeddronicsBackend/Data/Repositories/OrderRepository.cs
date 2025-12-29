using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Order>> GetOrdersByClientIdAsync(int clientId)
    {
        return await _dbSet
            .Where(o => o.ClientId == clientId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(string status)
    {
        return await _dbSet
            .Where(o => o.Status == status)
            .Include(o => o.Client)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(o => o.Client)
            .Include(o => o.Messages)
                .ThenInclude(m => m.Sender)
            .Include(o => o.Documents)
            .Include(o => o.Quotes)
            .Include(o => o.Invoices)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<Order>> GetOrdersWithMessagesAsync(int clientId)
    {
        return await _dbSet
            .Where(o => o.ClientId == clientId)
            .Include(o => o.Messages)
                .ThenInclude(m => m.Sender)
            .OrderByDescending(o => o.UpdatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersWithQuotesAsync(int clientId)
    {
        return await _dbSet
            .Where(o => o.ClientId == clientId)
            .Include(o => o.Quotes)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateOrderStatusAsync(int id, string status)
    {
        var order = await _dbSet.FindAsync(id);
        if (order == null)
            return false;

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
            .Include(o => o.Client)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetOrderCountByStatusAsync(string status)
    {
        return await _dbSet.CountAsync(o => o.Status == status);
    }
}