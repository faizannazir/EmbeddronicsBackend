using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmbeddronicsBackend.Data.Repositories;

public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(EmbeddronicsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Message>> GetMessagesByOrderIdAsync(int orderId)
    {
        return await _dbSet
            .Where(m => m.OrderId == orderId)
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId)
    {
        return await _dbSet
            .Where(m => !m.IsRead && m.SenderId != userId)
            .Include(m => m.Sender)
            .Include(m => m.Order)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Message>> GetMessagesByOrderIdPagedAsync(int orderId, int page, int pageSize)
    {
        return await _dbSet
            .Where(m => m.OrderId == orderId)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<bool> MarkMessagesAsReadAsync(int orderId, int userId)
    {
        var messages = await _dbSet
            .Where(m => m.OrderId == orderId && m.SenderId != userId && !m.IsRead)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsRead = true;
        }

        return messages.Any();
    }

    public async Task<int> GetUnreadMessageCountAsync(int orderId, int userId)
    {
        return await _dbSet
            .CountAsync(m => m.OrderId == orderId && m.SenderId != userId && !m.IsRead);
    }

    public async Task<Message?> GetLatestMessageByOrderAsync(int orderId)
    {
        return await _dbSet
            .Where(m => m.OrderId == orderId)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Message>> GetMessagesWithAttachmentsAsync(int orderId)
    {
        return await _dbSet
            .Where(m => m.OrderId == orderId && m.Attachments != null)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }
}