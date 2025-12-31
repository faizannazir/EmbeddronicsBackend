using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace EmbeddronicsBackend.Services.Caching;

/// <summary>
/// In-memory cache service implementation for scenarios where Redis is not available
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keyTracker = new();

    public MemoryCacheService(
        IMemoryCache memoryCache,
        ILogger<MemoryCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache hit: {Key}", key);
            return Task.FromResult(value);
        }

        _logger.LogDebug("Cache miss: {Key}", key);
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();
        
        if (absoluteExpiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration;
        else
            options.AbsoluteExpirationRelativeToNow = CacheDurations.Medium;

        if (slidingExpiration.HasValue)
            options.SlidingExpiration = slidingExpiration;

        // Set size limit to prevent unbounded growth
        options.Size = 1;

        _memoryCache.Set(key, value, options);
        _keyTracker.TryAdd(key, 0);

        _logger.LogDebug("Cache set: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        _keyTracker.TryRemove(key, out _);
        _logger.LogDebug("Cache removed: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _keyTracker.Keys.Where(k => k.StartsWith(prefix)).ToList();
        
        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _keyTracker.TryRemove(key, out _);
        }

        _logger.LogDebug("Cache removed by prefix: {Prefix} ({Count} keys)", prefix, keysToRemove.Count);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_memoryCache.TryGetValue(key, out _));
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
            return cached;

        var value = await factory();
        if (value != null)
        {
            await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);
        }

        return value;
    }

    public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        // Memory cache doesn't support sliding expiration refresh directly
        // The sliding expiration is automatically handled by MemoryCache
        _logger.LogDebug("Cache refresh requested (no-op for memory cache): {Key}", key);
        return Task.CompletedTask;
    }
}
