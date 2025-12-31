using System.Text.Json;

namespace EmbeddronicsBackend.Services.Caching;

/// <summary>
/// Interface for caching service supporting both in-memory and distributed caching
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a value from cache
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set a value in cache with optional expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a value from cache
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove all values matching a pattern
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get or set a value using a factory function
    /// </summary>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refresh the expiration of a cached item
    /// </summary>
    Task RefreshAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache key constants for the application
/// </summary>
public static class CacheKeys
{
    public const string ProductsAll = "products:all";
    public const string ProductById = "products:id:{0}";
    public const string ProductsByCategory = "products:category:{0}";
    
    public const string ServicesAll = "services:all";
    public const string ServiceById = "services:id:{0}";
    
    public const string ProjectsAll = "projects:all";
    public const string ProjectById = "projects:id:{0}";
    public const string ProjectsFeatured = "projects:featured";
    
    public const string BlogPostsAll = "blog:all";
    public const string BlogPostById = "blog:id:{0}";
    public const string BlogPostsRecent = "blog:recent:{0}";
    
    public const string ReviewsAll = "reviews:all";
    public const string ReviewsFeatured = "reviews:featured";
    
    public const string UserById = "users:id:{0}";
    public const string UserByEmail = "users:email:{0}";
    
    public const string OrdersByClient = "orders:client:{0}";
    public const string QuotesByClient = "quotes:client:{0}";
    
    public static string GetProductKey(string id) => string.Format(ProductById, id);
    public static string GetProductsByCategoryKey(string category) => string.Format(ProductsByCategory, category);
    public static string GetServiceKey(string id) => string.Format(ServiceById, id);
    public static string GetProjectKey(string id) => string.Format(ProjectById, id);
    public static string GetBlogPostKey(string id) => string.Format(BlogPostById, id);
    public static string GetRecentBlogPostsKey(int count) => string.Format(BlogPostsRecent, count);
    public static string GetUserByIdKey(string id) => string.Format(UserById, id);
    public static string GetUserByEmailKey(string email) => string.Format(UserByEmail, email);
    public static string GetOrdersByClientKey(string clientId) => string.Format(OrdersByClient, clientId);
    public static string GetQuotesByClientKey(string clientId) => string.Format(QuotesByClient, clientId);
}

/// <summary>
/// Cache duration constants
/// </summary>
public static class CacheDurations
{
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Long = TimeSpan.FromHours(1);
    public static readonly TimeSpan VeryLong = TimeSpan.FromHours(6);
    public static readonly TimeSpan Day = TimeSpan.FromDays(1);
}
