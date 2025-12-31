using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Services
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderDto>> GetOrdersAsync(int? clientId = null);
        Task<OrderDto?> GetOrderByIdAsync(int id);
        Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, int clientId);
        Task<OrderDto?> UpdateOrderAsync(int id, UpdateOrderRequest request, int currentUserId);
        Task<bool> UpdateOrderStatusAsync(int id, string status, int currentUserId);
        Task<bool> DeleteOrderAsync(int id, int currentUserId);
        Task<IEnumerable<OrderDto>> GetOrdersByStatusAsync(string status);
        Task<IEnumerable<OrderDto>> GetOrdersByClientIdAsync(int clientId);
        Task<bool> CanUserAccessOrderAsync(int orderId, int userId, string userRole);
        Task<bool> IsValidStatusTransitionAsync(string currentStatus, string newStatus);
        Task<IEnumerable<string>> GetValidStatusTransitionsAsync(string currentStatus);
    }
}