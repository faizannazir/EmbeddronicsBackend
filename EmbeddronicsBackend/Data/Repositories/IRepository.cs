using System.Linq.Expressions;

namespace EmbeddronicsBackend.Data.Repositories;

public interface IRepository<T> where T : class
{
    // Query operations
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(int id, params Expression<Func<T, object>>[] includes);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);

    // Pagination
    Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize);
    Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, params Expression<Func<T, object>>[] includes);
    Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);

    // Command operations
    Task<T> AddAsync(T entity);
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities);
    Task<T> UpdateAsync(T entity);
    Task<IEnumerable<T>> UpdateRangeAsync(IEnumerable<T> entities);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeleteAsync(T entity);
    Task<int> DeleteRangeAsync(IEnumerable<T> entities);
    Task<int> DeleteRangeAsync(Expression<Func<T, bool>> predicate);
}