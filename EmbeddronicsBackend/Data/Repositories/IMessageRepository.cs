using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IMessageRepository : IRepository<Message>
{
    Task<IEnumerable<Message>> GetMessagesByOrderIdAsync(int orderId);
    Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId);
    Task<IEnumerable<Message>> GetMessagesByOrderIdPagedAsync(int orderId, int page, int pageSize);
    Task<bool> MarkMessagesAsReadAsync(int orderId, int userId);
    Task<int> GetUnreadMessageCountAsync(int orderId, int userId);
    Task<Message?> GetLatestMessageByOrderAsync(int orderId);
    Task<IEnumerable<Message>> GetMessagesWithAttachmentsAsync(int orderId);
}