using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Data.Repositories;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Models.Exceptions;
using EmbeddronicsBackend.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EmbeddronicsBackend.Services
{
    public class QuoteService : IQuoteService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IQuoteRepository _quoteRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly ILogger<QuoteService> _logger;

        // Pricing configuration - in a real application, this would come from configuration or database
        private readonly Dictionary<string, decimal> _discountRules = new()
        {
            { "volume_10", 0.05m },    // 5% discount for 10+ items
            { "volume_50", 0.10m },    // 10% discount for 50+ items
            { "volume_100", 0.15m },   // 15% discount for 100+ items
            { "loyal_customer", 0.08m }, // 8% discount for loyal customers
            { "bulk_order", 0.12m }    // 12% discount for bulk orders over $10,000
        };

        private readonly Dictionary<string, decimal> _taxRates = new()
        {
            { "USD", 0.08m },  // 8% tax for USD
            { "EUR", 0.20m },  // 20% VAT for EUR
            { "GBP", 0.20m },  // 20% VAT for GBP
            { "CAD", 0.13m }   // 13% tax for CAD
        };

        public QuoteService(
            IUnitOfWork unitOfWork,
            IQuoteRepository quoteRepository,
            IUserRepository userRepository,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            ILogger<QuoteService> logger)
        {
            _unitOfWork = unitOfWork;
            _quoteRepository = quoteRepository;
            _userRepository = userRepository;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<QuoteDto>> GetQuotesAsync(int? clientId = null)
        {
            try
            {
                IEnumerable<Quote> quotes;
                
                if (clientId.HasValue)
                {
                    quotes = await _quoteRepository.GetQuotesByClientIdAsync(clientId.Value);
                }
                else
                {
                    quotes = await _quoteRepository.GetAllAsync();
                }

                return quotes.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes for client {ClientId}", clientId);
                throw;
            }
        }

        public async Task<QuoteDto?> GetQuoteByIdAsync(int id)
        {
            try
            {
                var quote = await _quoteRepository.GetQuoteWithItemsAsync(id);
                return quote != null ? MapToDto(quote) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quote {QuoteId}", id);
                throw;
            }
        }

        public async Task<QuoteDto> CreateQuoteAsync(CreateQuoteRequest request, int currentUserId)
        {
            try
            {
                // Validate client exists
                var client = await _userRepository.GetByIdAsync(request.ClientId);
                if (client == null)
                {
                    throw new NotFoundException($"Client with ID {request.ClientId} not found");
                }

                // Validate order exists if provided
                Order? order = null;
                if (request.OrderId.HasValue)
                {
                    order = await _orderRepository.GetByIdAsync(request.OrderId.Value);
                    if (order == null)
                    {
                        throw new NotFoundException($"Order with ID {request.OrderId} not found");
                    }
                }

                // Calculate quote amount if items are provided
                decimal calculatedAmount = request.Amount;
                if (request.Items != null && request.Items.Any())
                {
                    calculatedAmount = await CalculateQuoteTotalAsync(request.Items);
                    
                    // Apply discounts
                    var discountAmount = await CalculateDiscountsAsync(request.Items, request.ClientId, calculatedAmount);
                    calculatedAmount -= discountAmount;
                }

                // Create quote entity
                var quote = new Quote
                {
                    ClientId = request.ClientId,
                    OrderId = request.OrderId,
                    Amount = calculatedAmount,
                    Currency = request.Currency,
                    Status = "pending",
                    ValidUntil = request.ValidUntil,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _quoteRepository.AddAsync(quote);
                await _unitOfWork.SaveChangesAsync();

                // Add quote items if provided
                if (request.Items != null && request.Items.Any())
                {
                    await AddQuoteItemsAsync(quote.Id, request.Items);
                }

                _logger.LogInformation("Quote {QuoteId} created for client {ClientId} by user {UserId}", 
                    quote.Id, request.ClientId, currentUserId);

                // Return the complete quote with items
                var createdQuote = await _quoteRepository.GetQuoteWithItemsAsync(quote.Id);
                return MapToDto(createdQuote!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quote for client {ClientId}", request.ClientId);
                throw;
            }
        }   
     public async Task<QuoteDto?> UpdateQuoteAsync(int id, UpdateQuoteRequest request, int currentUserId)
        {
            try
            {
                var quote = await _quoteRepository.GetQuoteWithItemsAsync(id);
                if (quote == null)
                {
                    return null;
                }

                // Validate status transition if status is being updated
                if (!string.IsNullOrEmpty(request.Status) && request.Status != quote.Status)
                {
                    var validTransitions = await GetValidStatusTransitionsAsync(quote.Status);
                    if (!validTransitions.Contains(request.Status))
                    {
                        throw new ValidationException($"Invalid status transition from {quote.Status} to {request.Status}");
                    }
                }

                // Update quote properties
                if (request.Amount.HasValue)
                    quote.Amount = request.Amount.Value;
                
                if (!string.IsNullOrEmpty(request.Currency))
                    quote.Currency = request.Currency;
                
                if (request.ValidUntil.HasValue)
                    quote.ValidUntil = request.ValidUntil.Value;
                
                if (!string.IsNullOrEmpty(request.Status))
                    quote.Status = request.Status;

                quote.UpdatedAt = DateTime.UtcNow;

                // Update quote items if provided
                if (request.Items != null)
                {
                    // Remove existing items
                    var existingItems = quote.Items.ToList();
                    foreach (var item in existingItems)
                    {
                        quote.Items.Remove(item);
                    }

                    // Recalculate amount based on new items
                    var calculatedAmount = await CalculateQuoteTotalAsync(request.Items);
                    var discountAmount = await CalculateDiscountsAsync(request.Items, quote.ClientId, calculatedAmount);
                    quote.Amount = calculatedAmount - discountAmount;

                    // Add new items
                    await AddQuoteItemsAsync(quote.Id, request.Items);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} updated by user {UserId}", id, currentUserId);

                return MapToDto(quote);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quote {QuoteId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteQuoteAsync(int id, int currentUserId)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(id);
                if (quote == null)
                {
                    return false;
                }

                // Only allow deletion of pending quotes
                if (quote.Status != "pending")
                {
                    throw new ValidationException("Only pending quotes can be deleted");
                }

                await _quoteRepository.DeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} deleted by user {UserId}", id, currentUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting quote {QuoteId}", id);
                throw;
            }
        }

        public async Task<bool> AcceptQuoteAsync(int id, int clientId)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(id);
                if (quote == null)
                {
                    return false;
                }

                // Validate client ownership
                if (quote.ClientId != clientId)
                {
                    throw new UnauthorizedAccessException("Client can only accept their own quotes");
                }

                // Validate quote status and expiration
                if (quote.Status != "pending")
                {
                    throw new ValidationException("Only pending quotes can be accepted");
                }

                if (await IsQuoteExpiredAsync(id))
                {
                    throw new ValidationException("Cannot accept an expired quote");
                }

                // Update quote status
                quote.Status = "approved";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} accepted by client {ClientId}", id, clientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting quote {QuoteId} by client {ClientId}", id, clientId);
                throw;
            }
        }

        public async Task<bool> RejectQuoteAsync(int id, int clientId, string? notes = null)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(id);
                if (quote == null)
                {
                    return false;
                }

                // Validate client ownership
                if (quote.ClientId != clientId)
                {
                    throw new UnauthorizedAccessException("Client can only reject their own quotes");
                }

                // Validate quote status
                if (quote.Status != "pending")
                {
                    throw new ValidationException("Only pending quotes can be rejected");
                }

                // Update quote status
                quote.Status = "rejected";
                quote.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Quote {QuoteId} rejected by client {ClientId}. Notes: {Notes}", 
                    id, clientId, notes ?? "None");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting quote {QuoteId} by client {ClientId}", id, clientId);
                throw;
            }
        }

        public async Task<IEnumerable<QuoteDto>> GetQuotesByStatusAsync(string status)
        {
            try
            {
                var quotes = await _quoteRepository.GetQuotesByStatusAsync(status);
                return quotes.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes by status {Status}", status);
                throw;
            }
        }

        public async Task<IEnumerable<QuoteDto>> GetQuotesByClientIdAsync(int clientId)
        {
            try
            {
                var quotes = await _quoteRepository.GetQuotesByClientIdAsync(clientId);
                return quotes.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes for client {ClientId}", clientId);
                throw;
            }
        }

        public async Task<IEnumerable<QuoteDto>> GetQuotesByOrderIdAsync(int orderId)
        {
            try
            {
                var quotes = await _quoteRepository.GetQuotesByOrderIdAsync(orderId);
                return quotes.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quotes for order {OrderId}", orderId);
                throw;
            }
        } 
       public async Task<decimal> CalculateQuoteTotalAsync(List<CreateQuoteItemRequest> items)
        {
            try
            {
                decimal total = 0;

                foreach (var item in items)
                {
                    // Get product pricing if product ID is provided
                    decimal unitPrice = item.UnitPrice;
                    
                    if (item.ProductId.HasValue)
                    {
                        var product = await _productRepository.GetByIdAsync(item.ProductId.Value);
                        if (product != null && product.Price.HasValue)
                        {
                            // Use product price if item price is not specified or is zero
                            if (item.UnitPrice <= 0)
                            {
                                unitPrice = product.Price.Value;
                            }
                        }
                    }

                    var itemTotal = unitPrice * item.Quantity;
                    total += itemTotal;
                }

                return total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating quote total");
                throw;
            }
        }

        public async Task<bool> IsQuoteExpiredAsync(int quoteId)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                return quote.ValidUntil < DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if quote {QuoteId} is expired", quoteId);
                throw;
            }
        }

        public async Task<int> UpdateExpiredQuotesAsync()
        {
            try
            {
                var expiredQuotes = await _quoteRepository.GetExpiredQuotesAsync();
                int updatedCount = 0;

                foreach (var quote in expiredQuotes)
                {
                    quote.Status = "expired";
                    quote.UpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }

                if (updatedCount > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} expired quotes to expired status", updatedCount);
                }

                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expired quotes");
                throw;
            }
        }

        public async Task<bool> CanUserAccessQuoteAsync(int quoteId, int userId, string userRole)
        {
            try
            {
                var quote = await _quoteRepository.GetByIdAsync(quoteId);
                if (quote == null)
                {
                    return false;
                }

                // Admins can access all quotes
                if (userRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Clients can only access their own quotes
                if (userRole.Equals("client", StringComparison.OrdinalIgnoreCase))
                {
                    return quote.ClientId == userId;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user access for quote {QuoteId}", quoteId);
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetValidStatusTransitionsAsync(string currentStatus)
        {
            // Define valid status transitions based on business rules
            var transitions = new Dictionary<string, string[]>
            {
                { "pending", new[] { "approved", "rejected", "expired" } },
                { "approved", new[] { "completed", "cancelled" } },
                { "rejected", new string[] { } }, // No transitions from rejected
                { "expired", new string[] { } },  // No transitions from expired
                { "completed", new string[] { } }, // No transitions from completed
                { "cancelled", new string[] { } }  // No transitions from cancelled
            };

            return await Task.FromResult(
                transitions.TryGetValue(currentStatus.ToLower(), out var validTransitions) 
                    ? validTransitions 
                    : Array.Empty<string>()
            );
        }

        // Private helper methods

        private async Task<decimal> CalculateDiscountsAsync(List<CreateQuoteItemRequest> items, int clientId, decimal subtotal)
        {
            decimal totalDiscount = 0;

            try
            {
                // Volume discount based on total quantity
                var totalQuantity = items.Sum(i => i.Quantity);
                if (totalQuantity >= 100)
                {
                    totalDiscount += subtotal * _discountRules["volume_100"];
                }
                else if (totalQuantity >= 50)
                {
                    totalDiscount += subtotal * _discountRules["volume_50"];
                }
                else if (totalQuantity >= 10)
                {
                    totalDiscount += subtotal * _discountRules["volume_10"];
                }

                // Bulk order discount
                if (subtotal >= 10000)
                {
                    totalDiscount += subtotal * _discountRules["bulk_order"];
                }

                // Loyal customer discount (check if client has previous approved quotes)
                var clientQuotes = await _quoteRepository.GetQuotesByClientIdAsync(clientId);
                var approvedQuotesCount = clientQuotes.Count(q => q.Status == "approved");
                if (approvedQuotesCount >= 3)
                {
                    totalDiscount += subtotal * _discountRules["loyal_customer"];
                }

                // Ensure discount doesn't exceed 25% of subtotal
                var maxDiscount = subtotal * 0.25m;
                return Math.Min(totalDiscount, maxDiscount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating discounts for client {ClientId}, proceeding without discounts", clientId);
                return 0;
            }
        }

        private async Task AddQuoteItemsAsync(int quoteId, List<CreateQuoteItemRequest> items)
        {
            foreach (var itemRequest in items)
            {
                decimal unitPrice = itemRequest.UnitPrice;
                
                // Get product price if product ID is provided and unit price is not set
                if (itemRequest.ProductId.HasValue && unitPrice <= 0)
                {
                    var product = await _productRepository.GetByIdAsync(itemRequest.ProductId.Value);
                    if (product != null && product.Price.HasValue)
                    {
                        unitPrice = product.Price.Value;
                    }
                }

                var quoteItem = new QuoteItem
                {
                    QuoteId = quoteId,
                    ProductId = itemRequest.ProductId,
                    Description = itemRequest.Description,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * itemRequest.Quantity
                };

                // Add quote item through generic repository
                await _unitOfWork.Repository<QuoteItem>().AddAsync(quoteItem);
            }

            await _unitOfWork.SaveChangesAsync();
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