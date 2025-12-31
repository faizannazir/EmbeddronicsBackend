using EmbeddronicsBackend.Models.DTOs;

namespace EmbeddronicsBackend.Services
{
    /// <summary>
    /// Service for managing quote approval workflows and business processes
    /// </summary>
    public interface IQuoteWorkflowService
    {
        /// <summary>
        /// Initiates the quote approval workflow
        /// </summary>
        Task<bool> InitiateApprovalWorkflowAsync(int quoteId, int initiatedByUserId);

        /// <summary>
        /// Processes quote approval by admin
        /// </summary>
        Task<bool> ApproveQuoteAsync(int quoteId, int adminUserId, string? approvalNotes = null);

        /// <summary>
        /// Processes quote rejection by admin
        /// </summary>
        Task<bool> RejectQuoteAsync(int quoteId, int adminUserId, string rejectionReason);

        /// <summary>
        /// Sends quote to client for review
        /// </summary>
        Task<bool> SendQuoteToClientAsync(int quoteId, int adminUserId);

        /// <summary>
        /// Handles quote revision requests
        /// </summary>
        Task<QuoteDto?> RequestQuoteRevisionAsync(int quoteId, int clientId, string revisionNotes);

        /// <summary>
        /// Gets the current workflow status of a quote
        /// </summary>
        Task<QuoteWorkflowStatusDto> GetWorkflowStatusAsync(int quoteId);

        /// <summary>
        /// Gets quotes pending approval
        /// </summary>
        Task<IEnumerable<QuoteDto>> GetQuotesPendingApprovalAsync();

        /// <summary>
        /// Checks if a quote can be modified based on its current workflow status
        /// </summary>
        Task<bool> CanModifyQuoteAsync(int quoteId, int userId, string userRole);

        /// <summary>
        /// Handles automatic quote expiration notifications
        /// </summary>
        Task<int> SendExpirationNotificationsAsync();
    }
}