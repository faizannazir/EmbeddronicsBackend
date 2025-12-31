using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.Json;

namespace EmbeddronicsBackend.Services.Caching;

/// <summary>
/// Hybrid cache service that uses distributed cache (Redis) with in-memory L1 cache
/// Falls back to memory cache if distributed cache is unavailable
/// </summary>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keyTracker = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // L1 cache settings
    private readonly TimeSpan _l1CacheDuration = TimeSpan.FromSeconds(30);

    public DistributedCacheService(
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check L1 memory cache first
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                _logger.LogDebug("Cache hit (L1 memory): {Key}", key);
                return cachedValue;
            }

            // Check distributed cache
            var data = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(data))
            {
                _logger.LogDebug("Cache miss: {Key}", key);
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(data, _jsonOptions);
            
            // Store in L1 cache for faster subsequent access
            if (value != null)
            {
                _memoryCache.Set(key, value, _l1CacheDuration);
            }

            _logger.LogDebug("Cache hit (L2 distributed): {Key}", key);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions();
            
            if (absoluteExpiration.HasValue)
                options.AbsoluteExpirationRelativeToNow = absoluteExpiration;
            else
                options.AbsoluteExpirationRelativeToNow = CacheDurations.Medium;

            if (slidingExpiration.HasValue)
                options.SlidingExpiration = slidingExpiration;

            var data = JsonSerializer.Serialize(value, _jsonOptions);
            await _distributedCache.SetStringAsync(key, data, options, cancellationToken);

            // Store in L1 cache
            var l1Duration = absoluteExpiration.HasValue && absoluteExpiration.Value < _l1CacheDuration 
                ? absoluteExpiration.Value 
                : _l1CacheDuration;
            _memoryCache.Set(key, value, l1Duration);

            // Track the key for prefix-based removal
            _keyTracker.TryAdd(key, 0);

            _logger.LogDebug("Cache set: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
            _memoryCache.Remove(key);
            _keyTracker.TryRemove(key, out _);
            _logger.LogDebug("Cache removed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing from cache: {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var keysToRemove = _keyTracker.Keys.Where(k => k.StartsWith(prefix)).ToList();
            
            foreach (var key in keysToRemove)
            {
                await RemoveAsync(key, cancellationToken);
            }

            _logger.LogDebug("Cache removed by prefix: {Prefix} ({Count} keys)", prefix, keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing from cache by prefix: {Prefix}", prefix);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out _))
                return true;

            var data = await _distributedCache.GetStringAsync(key, cancellationToken);
            return !string.IsNullOrEmpty(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking cache existence: {Key}", key);
            return false;
        }
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

    public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RefreshAsync(key, cancellationToken);
            _logger.LogDebug("Cache refreshed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error refreshing cache: {Key}", key);
        }
    }
}
