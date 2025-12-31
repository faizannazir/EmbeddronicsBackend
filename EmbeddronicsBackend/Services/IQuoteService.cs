using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Services
{
    public interface IQuoteService
    {
        Task<IEnumerable<QuoteDto>> GetQuotesAsync(int? clientId = null);
        Task<QuoteDto?> GetQuoteByIdAsync(int id);
        Task<QuoteDto> CreateQuoteAsync(CreateQuoteRequest request, int currentUserId);
        Task<QuoteDto?> UpdateQuoteAsync(int id, UpdateQuoteRequest request, int currentUserId);
        Task<bool> DeleteQuoteAsync(int id, int currentUserId);
        Task<bool> AcceptQuoteAsync(int id, int clientId);
        Task<bool> RejectQuoteAsync(int id, int clientId, string? notes = null);
        Task<IEnumerable<QuoteDto>> GetQuotesByStatusAsync(string status);
        Task<IEnumerable<QuoteDto>> GetQuotesByClientIdAsync(int clientId);
        Task<IEnumerable<QuoteDto>> GetQuotesByOrderIdAsync(int orderId);
        Task<decimal> CalculateQuoteTotalAsync(List<CreateQuoteItemRequest> items);
        Task<bool> IsQuoteExpiredAsync(int quoteId);
        Task<int> UpdateExpiredQuotesAsync();
        Task<bool> CanUserAccessQuoteAsync(int quoteId, int userId, string userRole);
        Task<IEnumerable<string>> GetValidStatusTransitionsAsync(string currentStatus);
    }
}