using EmbeddronicsBackend.Data.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Concurrent;

namespace EmbeddronicsBackend.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly EmbeddronicsDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories;
    private IDbContextTransaction? _transaction;
    private bool _disposed = false;

    // Lazy-loaded repository properties
    private IUserRepository? _users;
    private IOrderRepository? _orders;
    private IQuoteRepository? _quotes;
    private IProductRepository? _products;
    private IMessageRepository? _messages;
    private IDocumentRepository? _documents;
    private IInvoiceRepository? _invoices;

    public UnitOfWork(EmbeddronicsDbContext context)
    {
        _context = context;
        _repositories = new ConcurrentDictionary<Type, object>();
    }

    // Repository properties with lazy initialization
    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IOrderRepository Orders => _orders ??= new OrderRepository(_context);
    public IQuoteRepository Quotes => _quotes ??= new QuoteRepository(_context);
    public IProductRepository Products => _products ??= new ProductRepository(_context);
    public IMessageRepository Messages => _messages ??= new MessageRepository(_context);
    public IDocumentRepository Documents => _documents ??= new DocumentRepository(_context);
    public IInvoiceRepository Invoices => _invoices ??= new InvoiceRepository(_context);

    // Generic repository access
    public IRepository<T> Repository<T>() where T : class
    {
        return (IRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new Repository<T>(_context));
    }

    // Transaction management
    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    // Transaction control
    public async Task BeginTransactionAsync()
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _transaction.RollbackAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    // Dispose pattern
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
            _disposed = true;
        }
    }
}