using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<IEnumerable<Invoice>> GetInvoicesByOrderIdAsync(int orderId);
    Task<IEnumerable<Invoice>> GetInvoicesByStatusAsync(string status);
    Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber);
    Task<IEnumerable<Invoice>> GetOverdueInvoicesAsync();
    Task<bool> UpdateInvoiceStatusAsync(int id, string status);
    Task<decimal> GetTotalInvoiceAmountByStatusAsync(string status);
    Task<IEnumerable<Invoice>> GetInvoicesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<Invoice?> GetInvoiceWithDetailsAsync(int id);
}