using EmbeddronicsBackend.Data.Repositories;

namespace EmbeddronicsBackend.Data;

public interface IUnitOfWork : IDisposable
{
    // Repository properties
    IUserRepository Users { get; }
    IOrderRepository Orders { get; }
    IQuoteRepository Quotes { get; }
    IProductRepository Products { get; }
    IMessageRepository Messages { get; }
    IDocumentRepository Documents { get; }
    IInvoiceRepository Invoices { get; }

    // Transaction management
    Task<int> SaveChangesAsync();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    
    // Transaction control
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    
    // Generic repository access for entities not covered by specific repositories
    IRepository<T> Repository<T>() where T : class;
}