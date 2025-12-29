using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Models.Entities;

namespace EmbeddronicsBackend.Examples;

/// <summary>
/// Example demonstrating how to use the Unit of Work pattern for transaction management
/// </summary>
public class UnitOfWorkExample
{
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkExample(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Example: Create an order with related entities in a single transaction
    /// </summary>
    public async Task<Order> CreateOrderWithQuoteAsync(int clientId, string title, string description, decimal quoteAmount)
    {
        try
        {
            // Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            // Create order
            var order = new Order
            {
                ClientId = clientId,
                Title = title,
                Description = description,
                Status = "new",
                BudgetRange = "$1000-$5000",
                Timeline = "2-4 weeks"
            };

            var createdOrder = await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            // Create quote for the order
            var quote = new Quote
            {
                ClientId = clientId,
                OrderId = createdOrder.Id,
                Amount = quoteAmount,
                Currency = "USD",
                Status = "pending",
                ValidUntil = DateTime.UtcNow.AddDays(30)
            };

            await _unitOfWork.Quotes.AddAsync(quote);
            await _unitOfWork.SaveChangesAsync();

            // Commit transaction
            await _unitOfWork.CommitTransactionAsync();

            return createdOrder;
        }
        catch (Exception)
        {
            // Rollback transaction on error
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Example: Using multiple repositories without explicit transaction
    /// </summary>
    public async Task<IEnumerable<Order>> GetClientOrdersWithDetailsAsync(int clientId)
    {
        // Get orders with related data
        var orders = await _unitOfWork.Orders.GetOrdersByClientIdAsync(clientId);
        
        // Get quotes for each order
        foreach (var order in orders)
        {
            var quotes = await _unitOfWork.Quotes.GetQuotesByOrderIdAsync(order.Id);
            // Process quotes as needed
        }

        return orders;
    }

    /// <summary>
    /// Example: Bulk operations with transaction management
    /// </summary>
    public async Task ProcessExpiredQuotesAsync()
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            // Get all expired quotes
            var expiredQuotes = await _unitOfWork.Quotes.GetExpiredQuotesAsync();

            // Update their status
            foreach (var quote in expiredQuotes)
            {
                await _unitOfWork.Quotes.UpdateQuoteStatusAsync(quote.Id, "expired");
            }

            // Save all changes
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Example: Using generic repository for entities without specific repositories
    /// </summary>
    public async Task<T> CreateGenericEntityAsync<T>(T entity) where T : class
    {
        var repository = _unitOfWork.Repository<T>();
        var createdEntity = await repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return createdEntity;
    }
}