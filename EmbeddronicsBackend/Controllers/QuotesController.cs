using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Models.Exceptions;
using System.Security.Claims;

namespace EmbeddronicsBackend.Controllers
{
    /// <summary>
    /// Controller for managing quotes and quote workflows
    /// </summary>
    [Authorize]
    public class QuotesController : BaseApiController
    {
        private readonly IQuoteService _quoteService;
        private readonly IQuoteWorkflowService _workflowService;
        private readonly ILogger<QuotesController> _logger;

        public QuotesController(
            IQuoteService quoteService,
            IQuoteWorkflowService workflowService,
            ILogger<QuotesController> logger)
        {
            _quoteService = quoteService;
            _workflowService = workflowService;
            _logger = logger;
        }

        /// <summary>
        /// Get all quotes (admin) or user's quotes (client)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<QuoteDto>>>> GetQuotes([FromQuery] int? clientId = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                // Clients can only see their own quotes
                if (userRole == "client")
                {
                    clientId = currentUserId;
                }

                var quotes = await _quoteService.GetQuotesAsync(clientId);
                return Success(quotes, "Quotes retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes");
                return InternalServerError<IEnumerable<QuoteDto>>("Failed to retrieve quotes");
            }
        }

        /// <summary>
        /// Get a specific quote by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<QuoteDto>>> GetQuote(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                // Check if user can access this quote
                if (!await _quoteService.CanUserAccessQuoteAsync(id, currentUserId, userRole))
                {
                    return Forbidden<QuoteDto>("You don't have permission to access this quote");
                }

                var quote = await _quoteService.GetQuoteByIdAsync(id);
                if (quote == null)
                {
                    return NotFound<QuoteDto>("Quote not found");
                }

                return Success(quote, "Quote retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quote {QuoteId}", id);
                return InternalServerError<QuoteDto>("Failed to retrieve quote");
            }
        }

        /// <summary>
        /// Create a new quote (admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<QuoteDto>>> CreateQuote([FromBody] CreateQuoteRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var quote = await _quoteService.CreateQuoteAsync(request, currentUserId);
                
                return Success(quote, "Quote created successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest<QuoteDto>(ex.Message);
            }
            catch (NotFoundException ex)
            {
                return NotFound<QuoteDto>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quote");
                return InternalServerError<QuoteDto>("Failed to create quote");
            }
        }

        /// <summary>
        /// Update an existing quote
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<QuoteDto>>> UpdateQuote(int id, [FromBody] UpdateQuoteRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                // Check if user can modify this quote
                if (!await _workflowService.CanModifyQuoteAsync(id, currentUserId, userRole))
                {
                    return Forbidden<QuoteDto>("You don't have permission to modify this quote");
                }

                var quote = await _quoteService.UpdateQuoteAsync(id, request, currentUserId);
                if (quote == null)
                {
                    return NotFound<QuoteDto>("Quote not found");
                }

                return Success(quote, "Quote updated successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest<QuoteDto>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbidden<QuoteDto>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quote {QuoteId}", id);
                return InternalServerError<QuoteDto>("Failed to update quote");
            }
        }

        /// <summary>
        /// Delete a quote (admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse>> DeleteQuote(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var success = await _quoteService.DeleteQuoteAsync(id, currentUserId);
                
                if (!success)
                {
                    return NotFound("Quote not found");
                }

                return Success("Quote deleted successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting quote {QuoteId}", id);
                return StatusCode(500, ApiResponse.InternalServerErrorResponse("Failed to delete quote"));
            }
        }        
/// <summary>
        /// Accept a quote (client only)
        /// </summary>
        [HttpPost("{id}/accept")]
        [Authorize(Roles = "client")]
        public async Task<ActionResult<ApiResponse>> AcceptQuote(int id, [FromBody] QuoteAcceptanceRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var success = await _quoteService.AcceptQuoteAsync(id, currentUserId);
                
                if (!success)
                {
                    return NotFound("Quote not found");
                }

                return Success("Quote accepted successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse.ForbiddenResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting quote {QuoteId}", id);
                return StatusCode(500, ApiResponse.InternalServerErrorResponse("Failed to accept quote"));
            }
        }

        /// <summary>
        /// Reject a quote (client only)
        /// </summary>
        [HttpPost("{id}/reject")]
        [Authorize(Roles = "client")]
        public async Task<ActionResult<ApiResponse>> RejectQuote(int id, [FromBody] QuoteAcceptanceRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var success = await _quoteService.RejectQuoteAsync(id, currentUserId, request.Notes);
                
                if (!success)
                {
                    return NotFound("Quote not found");
                }

                return Success("Quote rejected successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse.ForbiddenResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting quote {QuoteId}", id);
                return StatusCode(500, ApiResponse.InternalServerErrorResponse("Failed to reject quote"));
            }
        }

        /// <summary>
        /// Get quotes by status (admin only)
        /// </summary>
        [HttpGet("status/{status}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<IEnumerable<QuoteDto>>>> GetQuotesByStatus(string status)
        {
            try
            {
                var quotes = await _quoteService.GetQuotesByStatusAsync(status);
                return Success(quotes, $"Quotes with status '{status}' retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes by status {Status}", status);
                return InternalServerError<IEnumerable<QuoteDto>>("Failed to retrieve quotes");
            }
        }

        /// <summary>
        /// Get quotes for a specific order
        /// </summary>
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<QuoteDto>>>> GetQuotesByOrder(int orderId)
        {
            try
            {
                var quotes = await _quoteService.GetQuotesByOrderIdAsync(orderId);
                return Success(quotes, "Order quotes retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes for order {OrderId}", orderId);
                return InternalServerError<IEnumerable<QuoteDto>>("Failed to retrieve order quotes");
            }
        }

        /// <summary>
        /// Calculate quote total from items
        /// </summary>
        [HttpPost("calculate")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<decimal>>> CalculateQuoteTotal([FromBody] List<CreateQuoteItemRequest> items)
        {
            try
            {
                var total = await _quoteService.CalculateQuoteTotalAsync(items);
                return Success(total, "Quote total calculated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating quote total");
                return InternalServerError<decimal>("Failed to calculate quote total");
            }
        }

        // Workflow endpoints

        /// <summary>
        /// Submit quote for approval (admin only)
        /// </summary>
        [HttpPost("{id}/submit-for-approval")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse>> SubmitForApproval(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var success = await _workflowService.InitiateApprovalWorkflowAsync(id, currentUserId);
                
                if (!success)
                {
                    return NotFound("Quote not found");
                }

                return Success("Quote submitted for approval successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting quote {QuoteId} for approval", id);
                return StatusCode(500, ApiResponse.InternalServerErrorResponse("Failed to submit quote for approval"));
            }
        }

        /// <summary>
        /// Approve a quote (admin only)
        /// </summary>
        [HttpPost("{id}/approve")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse>> ApproveQuote(int id, [FromBody] QuoteApprovalRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                
                if (request.Approve)
                {
                    var success = await _workflowService.ApproveQuoteAsync(id, currentUserId, request.Notes);
                    if (!success)
                    {
                        return NotFound("Quote not found");
                    }
                    return Success("Quote approved successfully");
                }
                else
                {
                    var success = await _workflowService.RejectQuoteAsync(id, currentUserId, 
                        request.RejectionReason ?? "No reason provided");
                    if (!success)
                    {
                        return NotFound("Quote not found");
                    }
                    return Success("Quote rejected successfully");
                }
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse.ForbiddenResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing quote approval {QuoteId}", id);
                return StatusCode(500, ApiResponse.InternalServerErrorResponse("Failed to process quote approval"));
            }
        }

        /// <summary>
        /// Send approved quote to client (admin only)
        /// </summary>
        [HttpPost("{id}/send-to-client")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse>> SendToClient(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var success = await _workflowService.SendQuoteToClientAsync(id, currentUserId);
                
                if (!success)
                {
                    return NotFound("Quote not found");
                }

                return Success("Quote sent to client successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse.ForbiddenResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quote {QuoteId} to client", id);
                return StatusCode(500, ApiResponse.InternalServerErrorResponse("Failed to send quote to client"));
            }
        }

        /// <summary>
        /// Request quote revision (client only)
        /// </summary>
        [HttpPost("{id}/request-revision")]
        [Authorize(Roles = "client")]
        public async Task<ActionResult<ApiResponse<QuoteDto>>> RequestRevision(int id, [FromBody] QuoteRevisionRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var quote = await _workflowService.RequestQuoteRevisionAsync(id, currentUserId, request.RevisionNotes);
                
                if (quote == null)
                {
                    return NotFound<QuoteDto>("Quote not found");
                }

                return Success(quote, "Quote revision requested successfully");
            }
            catch (ValidationException ex)
            {
                return BadRequest<QuoteDto>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbidden<QuoteDto>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting revision for quote {QuoteId}", id);
                return InternalServerError<QuoteDto>("Failed to request quote revision");
            }
        }

        /// <summary>
        /// Get workflow status for a quote
        /// </summary>
        [HttpGet("{id}/workflow-status")]
        public async Task<ActionResult<ApiResponse<QuoteWorkflowStatusDto>>> GetWorkflowStatus(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                // Check if user can access this quote
                if (!await _quoteService.CanUserAccessQuoteAsync(id, currentUserId, userRole))
                {
                    return Forbidden<QuoteWorkflowStatusDto>("You don't have permission to access this quote");
                }

                var status = await _workflowService.GetWorkflowStatusAsync(id);
                return Success(status, "Workflow status retrieved successfully");
            }
            catch (NotFoundException ex)
            {
                return NotFound<QuoteWorkflowStatusDto>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving workflow status for quote {QuoteId}", id);
                return InternalServerError<QuoteWorkflowStatusDto>("Failed to retrieve workflow status");
            }
        }

        /// <summary>
        /// Get quotes pending approval (admin only)
        /// </summary>
        [HttpGet("pending-approval")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<IEnumerable<QuoteDto>>>> GetPendingApproval()
        {
            try
            {
                var quotes = await _workflowService.GetQuotesPendingApprovalAsync();
                return Success(quotes, "Pending approval quotes retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes pending approval");
                return InternalServerError<IEnumerable<QuoteDto>>("Failed to retrieve pending approval quotes");
            }
        }

        // Helper methods

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }
    }
}