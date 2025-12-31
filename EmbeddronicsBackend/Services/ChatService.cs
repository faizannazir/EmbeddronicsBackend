using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Service for chat operations including message persistence and retrieval
/// </summary>
public class ChatService : IChatService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ChatService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<ChatMessageDto> SaveMessageAsync(int senderId, SendMessageDto messageDto)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var chatMessage = new ChatMessage
        {
            SenderId = senderId,
            RecipientId = messageDto.RecipientId,
            OrderId = messageDto.OrderId,
            ChatRoom = messageDto.ChatRoom,
            Content = messageDto.Content,
            MessageType = messageDto.MessageType,
            Attachments = messageDto.Attachments,
            CreatedAt = DateTime.UtcNow
        };

        context.ChatMessages.Add(chatMessage);
        await context.SaveChangesAsync();

        // Load sender info for the DTO
        var sender = await context.Users.FindAsync(senderId);
        var recipient = messageDto.RecipientId.HasValue 
            ? await context.Users.FindAsync(messageDto.RecipientId.Value) 
            : null;

        Log.Information("Chat message {MessageId} saved from user {SenderId} to room {ChatRoom}", 
            chatMessage.Id, senderId, messageDto.ChatRoom);

        return new ChatMessageDto
        {
            Id = chatMessage.Id,
            SenderId = chatMessage.SenderId,
            SenderName = sender?.Name ?? "Unknown",
            SenderRole = sender?.Role ?? "client",
            RecipientId = chatMessage.RecipientId,
            RecipientName = recipient?.Name,
            OrderId = chatMessage.OrderId,
            ChatRoom = chatMessage.ChatRoom,
            Content = chatMessage.Content,
            MessageType = chatMessage.MessageType,
            Attachments = chatMessage.Attachments,
            IsRead = false,
            CreatedAt = chatMessage.CreatedAt
        };
    }

    public async Task<ChatMessageDto?> EditMessageAsync(int userId, EditMessageDto editDto)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == editDto.MessageId);

        if (message == null) return null;

        // Only the sender can edit their own message
        if (message.SenderId != userId) return null;

        message.Content = editDto.Content;
        message.IsEdited = true;
        message.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        Log.Information("Chat message {MessageId} edited by user {UserId}", editDto.MessageId, userId);

        return MapToDto(message);
    }

    public async Task<(bool Success, string Message, string? ChatRoom)> DeleteMessageAsync(int userId, int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages.FindAsync(messageId);
        if (message == null)
        {
            return (false, "Message not found", null);
        }

        // Check if user is the sender or an admin
        var user = await context.Users.FindAsync(userId);
        if (message.SenderId != userId && user?.Role != "admin")
        {
            return (false, "Unauthorized to delete this message", null);
        }

        // Soft delete
        message.IsDeleted = true;
        message.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        Log.Information("Chat message {MessageId} deleted by user {UserId}", messageId, userId);

        return (true, "Message deleted", message.ChatRoom);
    }

    public async Task MarkMessagesAsReadAsync(int userId, MarkMessagesReadDto dto)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var now = DateTime.UtcNow;

        if (dto.MessageIds.Any())
        {
            // Mark specific messages as read
            var messages = await context.ChatMessages
                .Where(m => dto.MessageIds.Contains(m.Id) && 
                           (m.RecipientId == userId || m.RecipientId == null) &&
                           !m.IsRead)
                .ToListAsync();

            foreach (var message in messages)
            {
                message.IsRead = true;
                message.ReadAt = now;
            }
        }
        else if (!string.IsNullOrEmpty(dto.ChatRoom))
        {
            // Mark all messages in room as read
            var messages = await context.ChatMessages
                .Where(m => m.ChatRoom == dto.ChatRoom && 
                           m.SenderId != userId &&
                           !m.IsRead)
                .ToListAsync();

            foreach (var message in messages)
            {
                message.IsRead = true;
                message.ReadAt = now;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<ChatHistoryResponseDto> GetChatHistoryAsync(int userId, ChatHistoryRequestDto request)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Validate access
        if (!await CanAccessChatRoomAsync(userId, request.ChatRoom))
        {
            return new ChatHistoryResponseDto
            {
                ChatRoom = request.ChatRoom,
                Messages = new List<ChatMessageDto>(),
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize,
                HasMore = false
            };
        }

        var query = context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.ChatRoom == request.ChatRoom && !m.IsDeleted)
            .AsQueryable();

        if (request.Before.HasValue)
        {
            query = query.Where(m => m.CreatedAt < request.Before.Value);
        }

        var totalCount = await query.CountAsync();

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.Name,
                SenderRole = m.Sender.Role,
                RecipientId = m.RecipientId,
                RecipientName = m.Recipient != null ? m.Recipient.Name : null,
                OrderId = m.OrderId,
                ChatRoom = m.ChatRoom,
                Content = m.Content,
                MessageType = m.MessageType,
                Attachments = m.Attachments,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                IsEdited = m.IsEdited,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .ToListAsync();

        // Reverse to get chronological order
        messages.Reverse();

        return new ChatHistoryResponseDto
        {
            ChatRoom = request.ChatRoom,
            Messages = messages,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            HasMore = (request.Page * request.PageSize) < totalCount
        };
    }

    public async Task<int> GetUnreadCountAsync(int userId, string? chatRoom = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var query = context.ChatMessages
            .Where(m => !m.IsDeleted && 
                       !m.IsRead && 
                       m.SenderId != userId);

        if (!string.IsNullOrEmpty(chatRoom))
        {
            query = query.Where(m => m.ChatRoom == chatRoom);
        }
        else
        {
            // Get rooms the user has access to
            var user = await context.Users.FindAsync(userId);
            if (user?.Role == "admin")
            {
                // Admins can see all unread messages
            }
            else
            {
                // Clients can only see messages in their rooms
                var userOrderIds = await context.Orders
                    .Where(o => o.ClientId == userId)
                    .Select(o => o.Id)
                    .ToListAsync();

                var allowedRooms = userOrderIds.Select(id => $"order_{id}").ToList();
                allowedRooms.Add($"user_{userId}");
                allowedRooms.Add($"support_{userId}");

                query = query.Where(m => allowedRooms.Contains(m.ChatRoom) || 
                                        m.RecipientId == userId);
            }
        }

        return await query.CountAsync();
    }

    public async Task<bool> CanAccessChatRoomAsync(int userId, string chatRoom)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        if (user == null) return false;

        // Admins can access all rooms
        if (user.Role == "admin") return true;

        // User's personal room
        if (chatRoom == $"user_{userId}") return true;

        // User's support room
        if (chatRoom == $"support_{userId}") return true;

        // Check if it's an order room the user owns
        if (chatRoom.StartsWith("order_"))
        {
            if (int.TryParse(chatRoom.Replace("order_", ""), out var orderId))
            {
                var hasAccess = await context.Orders
                    .AnyAsync(o => o.Id == orderId && o.ClientId == userId);
                return hasAccess;
            }
        }

        // Check direct message rooms
        if (chatRoom.StartsWith("dm_"))
        {
            var parts = chatRoom.Replace("dm_", "").Split('_');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out var user1Id) && int.TryParse(parts[1], out var user2Id))
                {
                    return userId == user1Id || userId == user2Id;
                }
            }
        }

        return false;
    }

    public async Task<List<ChatRoomDto>> GetUserChatRoomsAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        if (user == null) return new List<ChatRoomDto>();

        var rooms = new List<ChatRoomDto>();

        if (user.Role == "admin")
        {
            // Get all active order rooms
            var orderRooms = await context.Orders
                .Where(o => o.Status != "completed" && o.Status != "cancelled")
                .Select(o => new ChatRoomDto
                {
                    RoomId = $"order_{o.Id}",
                    RoomName = $"Order: {o.Title}",
                    RoomType = "order",
                    OrderId = o.Id
                })
                .ToListAsync();
            rooms.AddRange(orderRooms);

            // Get support rooms with activity
            var supportRooms = await context.ChatMessages
                .Where(m => m.ChatRoom.StartsWith("support_") && !m.IsDeleted)
                .Select(m => m.ChatRoom)
                .Distinct()
                .ToListAsync();

            foreach (var roomId in supportRooms)
            {
                var clientId = int.Parse(roomId.Replace("support_", ""));
                var client = await context.Users.FindAsync(clientId);
                rooms.Add(new ChatRoomDto
                {
                    RoomId = roomId,
                    RoomName = $"Support: {client?.Name ?? "Unknown"}",
                    RoomType = "support"
                });
            }
        }
        else
        {
            // Client rooms
            var orderRooms = await context.Orders
                .Where(o => o.ClientId == userId)
                .Select(o => new ChatRoomDto
                {
                    RoomId = $"order_{o.Id}",
                    RoomName = $"Order: {o.Title}",
                    RoomType = "order",
                    OrderId = o.Id
                })
                .ToListAsync();
            rooms.AddRange(orderRooms);

            // Support room
            rooms.Add(new ChatRoomDto
            {
                RoomId = $"support_{userId}",
                RoomName = "Support",
                RoomType = "support"
            });
        }

        // Add unread counts and last message
        foreach (var room in rooms)
        {
            room.UnreadCount = await GetUnreadCountAsync(userId, room.RoomId);
            
            var lastMessage = await context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.ChatRoom == room.RoomId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastMessage != null)
            {
                room.LastMessage = MapToDto(lastMessage);
            }
        }

        return rooms.OrderByDescending(r => r.LastMessage?.CreatedAt ?? DateTime.MinValue).ToList();
    }

    public async Task<ChatMessageDto?> GetMessageByIdAsync(int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

        return message != null ? MapToDto(message) : null;
    }

    public async Task<List<ChatMessageDto>> SearchMessagesAsync(int userId, string chatRoom, string searchTerm, int limit = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Validate access
        if (!await CanAccessChatRoomAsync(userId, chatRoom))
        {
            return new List<ChatMessageDto>();
        }

        return await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.ChatRoom == chatRoom && 
                       !m.IsDeleted && 
                       m.Content.Contains(searchTerm))
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.Name,
                SenderRole = m.Sender.Role,
                RecipientId = m.RecipientId,
                RecipientName = m.Recipient != null ? m.Recipient.Name : null,
                OrderId = m.OrderId,
                ChatRoom = m.ChatRoom,
                Content = m.Content,
                MessageType = m.MessageType,
                Attachments = m.Attachments,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                IsEdited = m.IsEdited,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .ToListAsync();
    }

    private static ChatMessageDto MapToDto(ChatMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = message.Sender?.Name ?? "Unknown",
            SenderRole = message.Sender?.Role ?? "client",
            RecipientId = message.RecipientId,
            RecipientName = message.Recipient?.Name,
            OrderId = message.OrderId,
            ParentMessageId = message.ParentMessageId,
            ConversationId = message.ConversationId,
            ChatRoom = message.ChatRoom,
            Content = message.Content,
            MessageType = message.MessageType,
            Priority = message.Priority,
            Attachments = message.Attachments,
            IsRead = message.IsRead,
            ReadAt = message.ReadAt,
            IsEdited = message.IsEdited,
            IsPinned = message.IsPinned,
            ReplyCount = message.ReplyCount,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt
        };
    }

    #region Threading & Conversation Management

    public async Task<ChatMessageDto> ReplyToMessageAsync(int senderId, int parentMessageId, SendMessageDto messageDto)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Get parent message
        var parentMessage = await context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == parentMessageId && !m.IsDeleted);

        if (parentMessage == null)
        {
            throw new InvalidOperationException("Parent message not found");
        }

        // Validate access
        if (!await CanAccessChatRoomAsync(senderId, parentMessage.ChatRoom))
        {
            throw new UnauthorizedAccessException("Access denied to this chat room");
        }

        // Determine conversation ID (use parent's conversation or parent's ID if it's a root message)
        var conversationId = parentMessage.ConversationId ?? parentMessage.Id;

        var chatMessage = new ChatMessage
        {
            SenderId = senderId,
            RecipientId = messageDto.RecipientId ?? parentMessage.SenderId,
            OrderId = parentMessage.OrderId,
            ParentMessageId = parentMessageId,
            ConversationId = conversationId,
            ChatRoom = parentMessage.ChatRoom,
            Content = messageDto.Content,
            MessageType = "reply",
            Priority = messageDto.Priority,
            Attachments = messageDto.Attachments,
            CreatedAt = DateTime.UtcNow
        };

        context.ChatMessages.Add(chatMessage);

        // Update parent message reply count
        parentMessage.ReplyCount++;

        await context.SaveChangesAsync();

        // Link attachments if provided
        if (messageDto.AttachmentIds?.Any() == true)
        {
            var attachmentService = scope.ServiceProvider.GetRequiredService<IChatAttachmentService>();
            await attachmentService.LinkAttachmentsToMessageAsync(chatMessage.Id, messageDto.AttachmentIds);
        }

        // Load sender info for the DTO
        var sender = await context.Users.FindAsync(senderId);
        var recipient = chatMessage.RecipientId.HasValue 
            ? await context.Users.FindAsync(chatMessage.RecipientId.Value) 
            : null;

        Log.Information("Reply {MessageId} created to parent {ParentId} in conversation {ConversationId}", 
            chatMessage.Id, parentMessageId, conversationId);

        return new ChatMessageDto
        {
            Id = chatMessage.Id,
            SenderId = chatMessage.SenderId,
            SenderName = sender?.Name ?? "Unknown",
            SenderRole = sender?.Role ?? "client",
            RecipientId = chatMessage.RecipientId,
            RecipientName = recipient?.Name,
            OrderId = chatMessage.OrderId,
            ParentMessageId = chatMessage.ParentMessageId,
            ConversationId = chatMessage.ConversationId,
            ChatRoom = chatMessage.ChatRoom,
            Content = chatMessage.Content,
            MessageType = chatMessage.MessageType,
            Priority = chatMessage.Priority,
            Attachments = chatMessage.Attachments,
            IsRead = false,
            CreatedAt = chatMessage.CreatedAt
        };
    }

    public async Task<ThreadDetailsDto?> GetThreadAsync(int userId, int rootMessageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var rootMessage = await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == rootMessageId && !m.IsDeleted);

        if (rootMessage == null) return null;

        // Validate access
        if (!await CanAccessChatRoomAsync(userId, rootMessage.ChatRoom))
        {
            return null;
        }

        // Get all replies in the conversation
        var conversationId = rootMessage.ConversationId ?? rootMessage.Id;
        var replies = await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.ConversationId == conversationId && m.Id != rootMessageId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Get unique participants
        var participantIds = new HashSet<int> { rootMessage.SenderId };
        if (rootMessage.RecipientId.HasValue) participantIds.Add(rootMessage.RecipientId.Value);
        foreach (var reply in replies)
        {
            participantIds.Add(reply.SenderId);
            if (reply.RecipientId.HasValue) participantIds.Add(reply.RecipientId.Value);
        }

        var participants = await context.Users
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => new UserPresenceDto
            {
                UserId = u.Id,
                UserName = u.Name,
                Role = u.Role
            })
            .ToListAsync();

        return new ThreadDetailsDto
        {
            RootMessageId = rootMessageId,
            RootMessage = MapToDto(rootMessage),
            Replies = replies.Select(MapToDto).ToList(),
            TotalReplies = replies.Count,
            Participants = participants
        };
    }

    public async Task<List<ChatMessageDto>> GetRepliesAsync(int userId, int parentMessageId, int page = 1, int pageSize = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var parentMessage = await context.ChatMessages.FindAsync(parentMessageId);
        if (parentMessage == null) return new List<ChatMessageDto>();

        // Validate access
        if (!await CanAccessChatRoomAsync(userId, parentMessage.ChatRoom))
        {
            return new List<ChatMessageDto>();
        }

        return await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.ParentMessageId == parentMessageId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => MapToDto(m))
            .ToListAsync();
    }

    public async Task<ChatMessageDto> CreateConversationAsync(int userId, CreateConversationDto dto)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Validate access
        if (!await CanAccessChatRoomAsync(userId, dto.ChatRoom))
        {
            throw new UnauthorizedAccessException("Access denied to this chat room");
        }

        // Create root message for the conversation
        var rootMessage = new ChatMessage
        {
            SenderId = userId,
            ChatRoom = dto.ChatRoom,
            Content = $"**{dto.Subject}**\n\n{dto.InitialMessage}",
            MessageType = "text",
            Priority = dto.Priority,
            CreatedAt = DateTime.UtcNow
        };

        context.ChatMessages.Add(rootMessage);
        await context.SaveChangesAsync();

        // Set ConversationId to its own ID (root of conversation)
        rootMessage.ConversationId = rootMessage.Id;
        await context.SaveChangesAsync();

        var sender = await context.Users.FindAsync(userId);

        Log.Information("Conversation {ConversationId} created by user {UserId} in room {ChatRoom}", 
            rootMessage.Id, userId, dto.ChatRoom);

        return new ChatMessageDto
        {
            Id = rootMessage.Id,
            SenderId = rootMessage.SenderId,
            SenderName = sender?.Name ?? "Unknown",
            SenderRole = sender?.Role ?? "client",
            ConversationId = rootMessage.Id,
            ChatRoom = rootMessage.ChatRoom,
            Content = rootMessage.Content,
            MessageType = rootMessage.MessageType,
            Priority = rootMessage.Priority,
            IsRead = false,
            ReplyCount = 0,
            CreatedAt = rootMessage.CreatedAt
        };
    }

    public async Task<bool> PinMessageAsync(int userId, PinMessageDto dto)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages.FindAsync(dto.MessageId);
        if (message == null) return false;

        // Only admin or message sender can pin
        var user = await context.Users.FindAsync(userId);
        if (user?.Role != "admin" && message.SenderId != userId)
        {
            return false;
        }

        message.IsPinned = dto.IsPinned;
        message.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        Log.Information("Message {MessageId} {Action} by user {UserId}", 
            dto.MessageId, dto.IsPinned ? "pinned" : "unpinned", userId);

        return true;
    }

    public async Task<List<ChatMessageDto>> GetPinnedMessagesAsync(int userId, string chatRoom)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        // Validate access
        if (!await CanAccessChatRoomAsync(userId, chatRoom))
        {
            return new List<ChatMessageDto>();
        }

        return await context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.ChatRoom == chatRoom && m.IsPinned && !m.IsDeleted)
            .OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            .Select(m => MapToDto(m))
            .ToListAsync();
    }

    #endregion

    #region Read Receipts

    public async Task<List<MessageReadReceiptDto>> GetReadReceiptsAsync(int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        return await context.MessageReadReceipts
            .Include(r => r.User)
            .Where(r => r.MessageId == messageId)
            .OrderBy(r => r.ReadAt)
            .Select(r => new MessageReadReceiptDto
            {
                UserId = r.UserId,
                UserName = r.User.Name,
                ReadAt = r.ReadAt
            })
            .ToListAsync();
    }

    public async Task<bool> MarkMessageAsReadWithReceiptAsync(int userId, int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var message = await context.ChatMessages.FindAsync(messageId);
        if (message == null || message.SenderId == userId) return false;

        // Check if receipt already exists
        var existingReceipt = await context.MessageReadReceipts
            .AnyAsync(r => r.MessageId == messageId && r.UserId == userId);

        if (!existingReceipt)
        {
            context.MessageReadReceipts.Add(new MessageReadReceipt
            {
                MessageId = messageId,
                UserId = userId,
                ReadAt = DateTime.UtcNow
            });

            // Update message read status
            if (!message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
        }

        return true;
    }

    #endregion
}
