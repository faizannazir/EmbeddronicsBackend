using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetOrdersByClientIdAsync(int clientId);
    Task<IEnumerable<Order>> GetOrdersByStatusAsync(string status);
    Task<Order?> GetOrderWithDetailsAsync(int id);
    Task<IEnumerable<Order>> GetOrdersWithMessagesAsync(int clientId);
    Task<IEnumerable<Order>> GetOrdersWithQuotesAsync(int clientId);
    Task<bool> UpdateOrderStatusAsync(int id, string status);
    Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<int> GetOrderCountByStatusAsync(string status);
}