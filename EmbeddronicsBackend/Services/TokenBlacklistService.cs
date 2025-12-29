using Microsoft.Extensions.Caching.Memory;

namespace EmbeddronicsBackend.Services
{
    public class TokenBlacklistService : ITokenBlacklistService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<TokenBlacklistService> _logger;
        private const string BLACKLIST_PREFIX = "blacklist_";

        public TokenBlacklistService(IMemoryCache cache, ILogger<TokenBlacklistService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task BlacklistTokenAsync(string jti, DateTime expiration)
        {
            var key = BLACKLIST_PREFIX + jti;
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiration,
                Priority = CacheItemPriority.Low
            };

            _cache.Set(key, true, cacheOptions);
            _logger.LogInformation("Token blacklisted: {Jti}", jti);
            
            return Task.CompletedTask;
        }

        public Task<bool> IsTokenBlacklistedAsync(string jti)
        {
            var key = BLACKLIST_PREFIX + jti;
            var isBlacklisted = _cache.TryGetValue(key, out _);
            
            return Task.FromResult(isBlacklisted);
        }

        public Task CleanupExpiredTokensAsync()
        {
            // Memory cache automatically removes expired entries
            // This method is here for interface consistency and future Redis implementation
            _logger.LogInformation("Token blacklist cleanup completed (automatic with MemoryCache)");
            return Task.CompletedTask;
        }
    }
}