using System.Collections.Concurrent;
using System.Net;

namespace EmbeddronicsBackend.Middleware;

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Number of requests allowed per time window
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Time window in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Enable/disable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Endpoints that should be exempt from rate limiting
    /// </summary>
    public List<string> ExemptEndpoints { get; set; } = new()
    {
        "/health",
        "/swagger"
    };

    /// <summary>
    /// Rate limit by IP address (default) or by user
    /// </summary>
    public bool LimitByUser { get; set; } = false;

    /// <summary>
    /// Rate limit for authenticated users (usually higher)
    /// </summary>
    public int AuthenticatedPermitLimit { get; set; } = 500;

    /// <summary>
    /// Rate limit for anonymous users
    /// </summary>
    public int AnonymousPermitLimit { get; set; } = 50;

    /// <summary>
    /// Endpoint-specific rate limits
    /// </summary>
    public Dictionary<string, EndpointRateLimit> EndpointLimits { get; set; } = new();
}

/// <summary>
/// Endpoint-specific rate limit configuration
/// </summary>
public class EndpointRateLimit
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

/// <summary>
/// Rate limiter entry for tracking requests
/// </summary>
public class RateLimitEntry
{
    public int RequestCount { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime LastRequest { get; set; }
}

/// <summary>
/// Middleware for API rate limiting to prevent abuse
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _rateLimitStore = new();
    private readonly Timer _cleanupTimer;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new RateLimitOptions();

        // Cleanup expired entries every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Skip rate limiting for exempt endpoints
        if (_options.ExemptEndpoints.Any(e => path.StartsWith(e.ToLower())))
        {
            await _next(context);
            return;
        }

        var clientIdentifier = GetClientIdentifier(context);
        var permitLimit = GetPermitLimit(context, path);
        var windowSeconds = GetWindowSeconds(path);

        var entry = _rateLimitStore.GetOrAdd(clientIdentifier, _ => new RateLimitEntry
        {
            RequestCount = 0,
            WindowStart = DateTime.UtcNow,
            LastRequest = DateTime.UtcNow
        });

        lock (entry)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if ((now - entry.WindowStart).TotalSeconds >= windowSeconds)
            {
                entry.RequestCount = 0;
                entry.WindowStart = now;
            }

            entry.RequestCount++;
            entry.LastRequest = now;

            // Calculate remaining requests
            var remaining = Math.Max(0, permitLimit - entry.RequestCount);
            var resetTime = entry.WindowStart.AddSeconds(windowSeconds);

            // Add rate limit headers
            context.Response.Headers["X-RateLimit-Limit"] = permitLimit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();

            if (entry.RequestCount > permitLimit)
            {
                var retryAfter = (int)(resetTime - now).TotalSeconds;
                context.Response.Headers["Retry-After"] = retryAfter.ToString();

                _logger.LogWarning(
                    "Rate limit exceeded for {ClientIdentifier}. Path: {Path}, Count: {Count}, Limit: {Limit}",
                    clientIdentifier, path, entry.RequestCount, permitLimit);

                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                
                var response = System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "Too many requests",
                    message = $"Rate limit exceeded. Please retry after {retryAfter} seconds.",
                    retryAfter = retryAfter
                });

                context.Response.WriteAsync(response).Wait();
                return;
            }
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // For authenticated users, use user ID if configured
        if (_options.LimitByUser && context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst("sub")?.Value ??
                        context.User.FindFirst("userId")?.Value ??
                        context.User.Identity.Name;
            
            if (!string.IsNullOrEmpty(userId))
                return $"user:{userId}";
        }

        // Fall back to IP address
        var ip = GetClientIpAddress(context);
        var path = context.Request.Path.Value ?? "/";
        
        return $"ip:{ip}:{GetEndpointKey(path)}";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (from reverse proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
                return ips[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string GetEndpointKey(string path)
    {
        // Normalize path for rate limiting (group similar endpoints)
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            // Group by first two segments (e.g., /api/products)
            return $"{segments[0]}/{segments[1]}";
        }
        return segments.Length > 0 ? segments[0] : "root";
    }

    private int GetPermitLimit(HttpContext context, string path)
    {
        // Check for endpoint-specific limits
        foreach (var endpointLimit in _options.EndpointLimits)
        {
            if (path.StartsWith(endpointLimit.Key.ToLower()))
                return endpointLimit.Value.PermitLimit;
        }

        // Use different limits for authenticated vs anonymous
        if (context.User.Identity?.IsAuthenticated == true)
            return _options.AuthenticatedPermitLimit;

        return _options.AnonymousPermitLimit;
    }

    private int GetWindowSeconds(string path)
    {
        // Check for endpoint-specific window
        foreach (var endpointLimit in _options.EndpointLimits)
        {
            if (path.StartsWith(endpointLimit.Key.ToLower()))
                return endpointLimit.Value.WindowSeconds;
        }

        return _options.WindowSeconds;
    }

    private void CleanupExpiredEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var keysToRemove = _rateLimitStore
            .Where(kvp => kvp.Value.LastRequest < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _rateLimitStore.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Rate limiter cleanup: removed {Count} expired entries", keysToRemove.Count);
        }
    }
}

/// <summary>
/// Extension methods for rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, RateLimitOptions options)
    {
        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, Action<RateLimitOptions> configure)
    {
        var options = new RateLimitOptions();
        configure(options);
        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }
}
