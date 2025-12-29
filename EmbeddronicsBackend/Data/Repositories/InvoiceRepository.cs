using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Invoice>> GetInvoicesByOrderIdAsync(int orderId)
    {
        return await _dbSet
            .Where(i => i.OrderId == orderId)
            .Include(i => i.Quote)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Invoice>> GetInvoicesByStatusAsync(string status)
    {
        return await _dbSet
            .Where(i => i.Status == status)
            .Include(i => i.Order)
                .ThenInclude(o => o.Client)
            .Include(i => i.Quote)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(i => i.Order)
                .ThenInclude(o => o.Client)
            .Include(i => i.Quote)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<IEnumerable<Invoice>> GetOverdueInvoicesAsync()
    {
        return await _dbSet
            .Where(i => i.DueDate < DateTime.UtcNow && i.Status == "pending")
            .Include(i => i.Order)
                .ThenInclude(o => o.Client)
            .Include(i => i.Quote)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
    }

    public async Task<bool> UpdateInvoiceStatusAsync(int id, string status)
    {
        var invoice = await _dbSet.FindAsync(id);
        if (invoice == null)
            return false;

        invoice.Status = status;
        invoice.UpdatedAt = DateTime.UtcNow;
        
        if (status == "paid" && invoice.PaidDate == null)
        {
            invoice.PaidDate = DateTime.UtcNow;
        }

        return true;
    }

    public async Task<decimal> GetTotalInvoiceAmountByStatusAsync(string status)
    {
        return await _dbSet
            .Where(i => i.Status == status)
            .SumAsync(i => i.Amount);
    }

    public async Task<IEnumerable<Invoice>> GetInvoicesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(i => i.CreatedAt >= startDate && i.CreatedAt <= endDate)
            .Include(i => i.Order)
                .ThenInclude(o => o.Client)
            .Include(i => i.Quote)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<Invoice?> GetInvoiceWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(i => i.Order)
                .ThenInclude(o => o.Client)
            .Include(i => i.Quote)
                .ThenInclude(q => q.Items)
                    .ThenInclude(qi => qi.Product)
            .FirstOrDefaultAsync(i => i.Id == id);
    }
}