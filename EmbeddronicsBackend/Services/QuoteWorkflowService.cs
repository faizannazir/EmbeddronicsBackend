using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Data.Repositories;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Models.Exceptions;
using Microsoft.Extensions.Logging;

namespace EmbeddronicsBackend.Services
{
    public class QuoteWorkflowService : IQuoteWorkflowService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IQuoteRepository _quoteRepository;
        private readonly IUserRepository _userRepository;
        private readonly IQuoteService _quoteService;
        private readonly ILogger<QuoteWorkflowService> _logger;

        // Workflow stages
        private readonly Dictionary<string, string> _workflowStages = new()
        {
            { "pending", "Draft/Pending Review" },
            { "under_review", "Under Admin Review" },
            { "approved", "Approved by Admin" },
            { "sent_to_client", "Sent to Client" },
            { "client_approved", "Accepted by Client" },
            { "rejected", "Rejected" },
            { "expired", "Expired" },
            { "revision_requested", "Revision Requested" }
        };

        public QuoteWorkflowService(
            IUnitOfWork unitOfWork,
            IQuoteRepository quoteRepository,
            IUserRepository userRepository,
            IQuoteService quoteService,
            ILogger<QuoteWorkflowService> logger)
        {
            _unitOfWork = unitOfWork;
            _quoteRepository = quoteRepository;
            _userRepository = userRepository;
            _quoteService = quoteService;
            _logger = logger;
        }

        public async Task<bool> InitiateApprovalWorkflowAsync(int quoteId, int initiatedByUserId)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                if (quote.Status != "pending")
                {
                    throw new ValidationException("Only pending quotes can be submitted for approval");
                }

                quote.Status = "under_review";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} submitted for approval by user {UserId}", 
                    quoteId, initiatedByUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating approval workflow for quote {QuoteId}", quoteId);
                throw;
            }
        }

        public async Task<bool> ApproveQuoteAsync(int quoteId, int adminUserId, string? approvalNotes = null)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                // Validate admin role
                var admin = await _userRepository.GetByIdAsync(adminUserId);
                if (admin == null || !admin.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Only admins can approve quotes");
                }

                if (quote.Status != "under_review" && quote.Status != "pending")
                {
                    throw new ValidationException($"Cannot approve quote with status {quote.Status}");
                }

                quote.Status = "approved";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} approved by admin {AdminId}. Notes: {Notes}", 
                    quoteId, adminUserId, approvalNotes ?? "None");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving quote {QuoteId} by admin {AdminId}", quoteId, adminUserId);
                throw;
            }
        }

        public async Task<bool> RejectQuoteAsync(int quoteId, int adminUserId, string rejectionReason)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                // Validate admin role
                var admin = await _userRepository.GetByIdAsync(adminUserId);
                if (admin == null || !admin.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Only admins can reject quotes");
                }

                if (quote.Status != "under_review" && quote.Status != "pending")
                {
                    throw new ValidationException($"Cannot reject quote with status {quote.Status}");
                }

                quote.Status = "rejected";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} rejected by admin {AdminId}. Reason: {Reason}", 
                    quoteId, adminUserId, rejectionReason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting quote {QuoteId} by admin {AdminId}", quoteId, adminUserId);
                throw;
            }
        }

        public async Task<bool> SendQuoteToClientAsync(int quoteId, int adminUserId)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                // Validate admin role
                var admin = await _userRepository.GetByIdAsync(adminUserId);
                if (admin == null || !admin.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Only admins can send quotes to clients");
                }

                if (quote.Status != "approved")
                {
                    throw new ValidationException("Only approved quotes can be sent to clients");
                }

                quote.Status = "sent_to_client";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} sent to client by admin {AdminId}", quoteId, adminUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quote {QuoteId} to client by admin {AdminId}", quoteId, adminUserId);
                throw;
            }
        }        
public async Task<QuoteDto?> RequestQuoteRevisionAsync(int quoteId, int clientId, string revisionNotes)
        {
            try
            {
                var quote = await _quoteRepository.GetQuoteWithItemsAsync(quoteId);
                if (quote == null)
                {
                    return null;
                }

                // Validate client ownership
                if (quote.ClientId != clientId)
                {
                    throw new UnauthorizedAccessException("Client can only request revisions for their own quotes");
                }

                if (quote.Status != "sent_to_client" && quote.Status != "approved")
                {
                    throw new ValidationException("Revisions can only be requested for quotes sent to client");
                }

                quote.Status = "revision_requested";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote revision requested for quote {QuoteId} by client {ClientId}. Notes: {Notes}", 
                    quoteId, clientId, revisionNotes);

                return MapToDto(quote);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting revision for quote {QuoteId} by client {ClientId}", quoteId, clientId);
                throw;
            }
        }

        public async Task<QuoteWorkflowStatusDto> GetWorkflowStatusAsync(int quoteId)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    throw new NotFoundException($"Quote with ID {quoteId} not found");
                }

                var availableActions = await GetAvailableActionsAsync(quote);
                var workflowStage = _workflowStages.TryGetValue(quote.Status, out var stage) ? stage : quote.Status;

                return new QuoteWorkflowStatusDto
                {
                    QuoteId = quoteId,
                    CurrentStatus = quote.Status,
                    WorkflowStage = workflowStage,
                    CanModify = await CanModifyQuoteInternalAsync(quote),
                    CanApprove = availableActions.Contains("approve"),
                    CanReject = availableActions.Contains("reject"),
                    CanAccept = availableActions.Contains("accept"),
                    AvailableActions = availableActions,
                    LastStatusChange = quote.UpdatedAt,
                    LastActionBy = null, // Would need to track this in a separate audit table
                    Notes = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow status for quote {QuoteId}", quoteId);
                throw;
            }
        }

        public async Task<IEnumerable<QuoteDto>> GetQuotesPendingApprovalAsync()
        {
            try
            {
                var quotes = await _quoteRepository.GetQuotesByStatusAsync("under_review");
                return quotes.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes pending approval");
                throw;
            }
        }

        public async Task<bool> CanModifyQuoteAsync(int quoteId, int userId, string userRole)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                return await CanModifyQuoteInternalAsync(quote, userId, userRole);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if quote {QuoteId} can be modified by user {UserId}", quoteId, userId);
                throw;
            }
        }

        public async Task<int> SendExpirationNotificationsAsync()
        {
            try
            {
                // Get quotes expiring in the next 24 hours
                var soonToExpireQuotes = await _quoteRepository.FindAsync(q => 
                    q.Status == "sent_to_client" && 
                    q.ValidUntil > DateTime.UtcNow && 
                    q.ValidUntil <= DateTime.UtcNow.AddDays(1));

                int notificationsSent = 0;

                foreach (var quote in soonToExpireQuotes)
                {
                    // In a real implementation, this would send email notifications
                    _logger.LogInformation("Quote {QuoteId} for client {ClientId} expires on {ExpirationDate}", 
                        quote.Id, quote.ClientId, quote.ValidUntil);
                    notificationsSent++;
                }

                return notificationsSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending expiration notifications");
                throw;
            }
        }

        // Private helper methods

        private async Task<List<string>> GetAvailableActionsAsync(Quote quote)
        {
            var actions = new List<string>();

            switch (quote.Status.ToLower())
            {
                case "pending":
                    actions.AddRange(new[] { "submit_for_review", "modify", "delete" });
                    break;
                case "under_review":
                    actions.AddRange(new[] { "approve", "reject", "request_changes" });
                    break;
                case "approved":
                    actions.AddRange(new[] { "send_to_client", "modify" });
                    break;
                case "sent_to_client":
                    actions.AddRange(new[] { "accept", "reject", "request_revision" });
                    if (!await _quoteService.IsQuoteExpiredAsync(quote.Id))
                    {
                        actions.Add("extend_expiration");
                    }
                    break;
                case "revision_requested":
                    actions.AddRange(new[] { "modify", "resubmit" });
                    break;
            }

            return actions;
        }

        private async Task<bool> CanModifyQuoteInternalAsync(Quote quote, int? userId = null, string? userRole = null)
        {
            // Quotes can be modified in certain statuses
            var modifiableStatuses = new[] { "pending", "revision_requested", "approved" };
            
            if (!modifiableStatuses.Contains(quote.Status))
            {
                return false;
            }

            // Additional role-based checks
            if (userId.HasValue && !string.IsNullOrEmpty(userRole))
            {
                if (userRole.Equals("client", StringComparison.OrdinalIgnoreCase))
                {
                    // Clients can only modify their own quotes and only in specific statuses
                    return quote.ClientId == userId.Value && 
                           (quote.Status == "pending" || quote.Status == "revision_requested");
                }
                
                if (userRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    // Admins can modify quotes in any modifiable status
                    return true;
                }
            }

            return true;
        }

        private QuoteDto MapToDto(Quote quote)
        {
            return new QuoteDto
            {
                Id = quote.Id,
                ClientId = quote.ClientId,
                ClientName = quote.Client?.Name ?? string.Empty,
                OrderId = quote.OrderId,
                OrderTitle = quote.Order?.Title,
                Amount = quote.Amount,
                Currency = quote.Currency,
                Status = quote.Status,
                ValidUntil = quote.ValidUntil,
                Items = quote.Items?.Select(MapQuoteItemToDto).ToList(),
                CreatedAt = quote.CreatedAt,
                UpdatedAt = quote.UpdatedAt
            };
        }

        private QuoteItemDto MapQuoteItemToDto(QuoteItem item)
        {
            return new QuoteItemDto
            {
                Id = item.Id,
                QuoteId = item.QuoteId,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            };
        }
    }
}