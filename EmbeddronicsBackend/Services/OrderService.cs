using EmbeddronicsBackend.Data;
using EmbeddronicsBackend.Data.Repositories;
using EmbeddronicsBackend.Models.DTOs;
using EmbeddronicsBackend.Models.Entities;
using EmbeddronicsBackend.Models.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EmbeddronicsBackend.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderRepository _orderRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<OrderService> _logger;

        // Valid order status transitions
        private readonly Dictionary<string, List<string>> _statusTransitions = new()
        {
            { "new", new List<string> { "in_progress", "cancelled" } },
            { "in_progress", new List<string> { "completed", "cancelled" } },
            { "completed", new List<string>() }, // No transitions from completed
            { "cancelled", new List<string>() }  // No transitions from cancelled
        };

        public OrderService(
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepository,
            IUserRepository userRepository,
            ILogger<OrderService> logger)
        {
            _unitOfWork = unitOfWork;
            _orderRepository = orderRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersAsync(int? clientId = null)
        {
            try
            {
                IEnumerable<Order> orders;
                
                if (clientId.HasValue)
                {
                    orders = await _orderRepository.GetOrdersByClientIdAsync(clientId.Value);
                }
                else
                {
                    orders = await _orderRepository.GetAllAsync();
                }

                return orders.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders. ClientId: {ClientId}", clientId);
                throw;
            }
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int id)
        {
            try
            {
                var order = await _orderRepository.GetOrderWithDetailsAsync(id);
                return order != null ? MapToDto(order) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order with ID: {OrderId}", id);
                throw;
            }
        }

        public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, int clientId)
        {
            try
            {
                // Validate client exists
                var client = await _userRepository.GetByIdAsync(clientId);
                if (client == null)
                {
                    throw new NotFoundException($"Client with ID {clientId} not found");
                }

                // Validate client role
                if (client.Role != "client")
                {
                    throw new ValidationException("Only clients can create orders");
                }

                var order = new Order
                {
                    ClientId = clientId,
                    Title = request.Title,
                    Description = request.Description,
                    PcbSpecs = request.PcbSpecs != null ? JsonSerializer.Serialize(request.PcbSpecs) : null,
                    BudgetRange = request.BudgetRange,
                    Timeline = request.Timeline,
                    Status = "new",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _orderRepository.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Order created successfully. OrderId: {OrderId}, ClientId: {ClientId}", 
                    order.Id, clientId);

                // Reload with client details
                var createdOrder = await _orderRepository.GetOrderWithDetailsAsync(order.Id);
                return MapToDto(createdOrder!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for client: {ClientId}", clientId);
                throw;
            }
        }

        public async Task<OrderDto?> UpdateOrderAsync(int id, UpdateOrderRequest request, int currentUserId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    throw new NotFoundException($"Order with ID {id} not found");
                }

                // Authorization check
                var currentUser = await _userRepository.GetByIdAsync(currentUserId);
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User not found");
                }

                // Only the client who owns the order or admin can update it
                if (currentUser.Role != "admin" && order.ClientId != currentUserId)
                {
                    throw new UnauthorizedAccessException("You don't have permission to update this order");
                }

                // Clients can only update certain fields and cannot change status directly
                if (currentUser.Role == "client")
                {
                    if (!string.IsNullOrEmpty(request.Status))
                    {
                        throw new UnauthorizedAccessException("Clients cannot change order status directly");
                    }
                }

                // Update fields
                if (!string.IsNullOrEmpty(request.Title))
                    order.Title = request.Title;
                
                if (request.Description != null)
                    order.Description = request.Description;
                
                if (request.PcbSpecs != null)
                    order.PcbSpecs = JsonSerializer.Serialize(request.PcbSpecs);
                
                if (!string.IsNullOrEmpty(request.BudgetRange))
                    order.BudgetRange = request.BudgetRange;
                
                if (!string.IsNullOrEmpty(request.Timeline))
                    order.Timeline = request.Timeline;

                // Status update with validation (admin only)
                if (!string.IsNullOrEmpty(request.Status) && currentUser.Role == "admin")
                {
                    if (!await IsValidStatusTransitionAsync(order.Status, request.Status))
                    {
                        throw new ValidationException($"Invalid status transition from {order.Status} to {request.Status}");
                    }
                    order.Status = request.Status;
                }

                order.UpdatedAt = DateTime.UtcNow;

                await _orderRepository.UpdateAsync(order);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Order updated successfully. OrderId: {OrderId}, UpdatedBy: {UserId}", 
                    id, currentUserId);

                // Reload with details
                var updatedOrder = await _orderRepository.GetOrderWithDetailsAsync(id);
                return MapToDto(updatedOrder!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order: {OrderId}", id);
                throw;
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int id, string status, int currentUserId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    throw new NotFoundException($"Order with ID {id} not found");
                }

                // Authorization check - only admin can change status
                var currentUser = await _userRepository.GetByIdAsync(currentUserId);
                if (currentUser == null || currentUser.Role != "admin")
                {
                    throw new UnauthorizedAccessException("Only administrators can change order status");
                }

                // Validate status transition
                if (!await IsValidStatusTransitionAsync(order.Status, status))
                {
                    throw new ValidationException($"Invalid status transition from {order.Status} to {status}");
                }

                var success = await _orderRepository.UpdateOrderStatusAsync(id, status);
                if (success)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Order status updated. OrderId: {OrderId}, OldStatus: {OldStatus}, NewStatus: {NewStatus}, UpdatedBy: {UserId}", 
                        id, order.Status, status, currentUserId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status. OrderId: {OrderId}, Status: {Status}", id, status);
                throw;
            }
        }

        public async Task<bool> DeleteOrderAsync(int id, int currentUserId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    throw new NotFoundException($"Order with ID {id} not found");
                }

                // Authorization check - only admin can delete orders
                var currentUser = await _userRepository.GetByIdAsync(currentUserId);
                if (currentUser == null || currentUser.Role != "admin")
                {
                    throw new UnauthorizedAccessException("Only administrators can delete orders");
                }

                // Business rule: Cannot delete completed orders
                if (order.Status == "completed")
                {
                    throw new ValidationException("Cannot delete completed orders");
                }

                await _orderRepository.DeleteAsync(order);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Order deleted successfully. OrderId: {OrderId}, DeletedBy: {UserId}", 
                    id, currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order: {OrderId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersByStatusAsync(string status)
        {
            try
            {
                var orders = await _orderRepository.GetOrdersByStatusAsync(status);
                return orders.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders by status: {Status}", status);
                throw;
            }
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersByClientIdAsync(int clientId)
        {
            try
            {
                var orders = await _orderRepository.GetOrdersByClientIdAsync(clientId);
                return orders.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for client: {ClientId}", clientId);
                throw;
            }
        }

        public async Task<bool> CanUserAccessOrderAsync(int orderId, int userId, string userRole)
        {
            try
            {
                // Admin can access all orders
                if (userRole == "admin")
                    return true;

                // Client can only access their own orders
                var order = await _orderRepository.GetByIdAsync(orderId);
                return order != null && order.ClientId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking order access. OrderId: {OrderId}, UserId: {UserId}", orderId, userId);
                throw;
            }
        }

        public async Task<bool> IsValidStatusTransitionAsync(string currentStatus, string newStatus)
        {
            await Task.CompletedTask; // Make async for future extensibility
            
            if (!_statusTransitions.ContainsKey(currentStatus))
                return false;

            return _statusTransitions[currentStatus].Contains(newStatus);
        }

        public async Task<IEnumerable<string>> GetValidStatusTransitionsAsync(string currentStatus)
        {
            await Task.CompletedTask; // Make async for future extensibility
            
            return _statusTransitions.ContainsKey(currentStatus) 
                ? _statusTransitions[currentStatus] 
                : new List<string>();
        }

        private static OrderDto MapToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                ClientId = order.ClientId,
                ClientName = order.Client?.Name ?? string.Empty,
                Title = order.Title,
                Status = order.Status,
                Description = order.Description,
                PcbSpecs = !string.IsNullOrEmpty(order.PcbSpecs) 
                    ? JsonSerializer.Deserialize<PcbSpecsDto>(order.PcbSpecs) 
                    : null,
                BudgetRange = order.BudgetRange,
                Timeline = order.Timeline,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Messages = order.Messages?.Select(m => new MessageDto
                {
                    Id = m.Id,
                    OrderId = m.OrderId,
                    SenderId = m.SenderId,
                    SenderName = m.Sender?.Name ?? string.Empty,
                    Content = m.Content,
                    IsRead = m.IsRead,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                Documents = order.Documents?.Select(d => new DocumentDto
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    FileUrl = d.FilePath, // Using FilePath as FileUrl
                    FileType = d.FileType,
                    FileSize = d.FileSize,
                    UploadedAt = d.CreatedAt // Using CreatedAt as UploadedAt
                }).ToList()
            };
        }
    }
}