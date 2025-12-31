namespace EmbeddronicsBackend.Models.Configuration;

/// <summary>
/// Configuration for caching services
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// Enable caching
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Use Redis for distributed caching (false = memory cache only)
    /// </summary>
    public bool UseRedis { get; set; } = false;

    /// <summary>
    /// Redis connection string
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis instance name prefix
    /// </summary>
    public string RedisInstanceName { get; set; } = "Embeddronics_";

    /// <summary>
    /// Default cache duration in minutes
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 30;

    /// <summary>
    /// L1 (memory) cache duration in seconds for hybrid caching
    /// </summary>
    public int L1CacheDurationSeconds { get; set; } = 30;
}

/// <summary>
/// Configuration for rate limiting
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default number of requests allowed per window
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Window size in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Requests per window for authenticated users
    /// </summary>
    public int AuthenticatedPermitLimit { get; set; } = 500;

    /// <summary>
    /// Requests per window for anonymous users
    /// </summary>
    public int AnonymousPermitLimit { get; set; } = 50;

    /// <summary>
    /// Endpoints exempt from rate limiting
    /// </summary>
    public List<string> ExemptEndpoints { get; set; } = new() { "/health", "/swagger" };

    /// <summary>
    /// Endpoint-specific rate limits
    /// </summary>
    public Dictionary<string, EndpointRateLimitSetting> EndpointLimits { get; set; } = new();
}

/// <summary>
/// Endpoint-specific rate limit setting
/// </summary>
public class EndpointRateLimitSetting
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

/// <summary>
/// Configuration for security headers
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Enable HSTS
    /// </summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>
    /// HSTS max age in seconds
    /// </summary>
    public int HstsMaxAgeSeconds { get; set; } = 31536000;

    /// <summary>
    /// Enable Cross-Origin policies
    /// </summary>
    public bool EnableCrossOriginPolicies { get; set; } = false;

    /// <summary>
    /// Custom Content-Security-Policy (null = use default)
    /// </summary>
    public string? ContentSecurityPolicy { get; set; }

    /// <summary>
    /// X-Frame-Options header value
    /// </summary>
    public string XFrameOptions { get; set; } = "DENY";
}

/// <summary>
/// Configuration for request limits
/// </summary>
public class RequestLimitSettings
{
    /// <summary>
    /// Maximum request body size in bytes (default: 50MB)
    /// </summary>
    public long MaxRequestBodySize { get; set; } = 52428800;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum file upload size in bytes (default: 25MB)
    /// </summary>
    public long MaxFileUploadSize { get; set; } = 26214400;

    /// <summary>
    /// Maximum query string length
    /// </summary>
    public int MaxQueryStringLength { get; set; } = 2048;

    /// <summary>
    /// Maximum URL length
    /// </summary>
    public int MaxUrlLength { get; set; } = 8192;
}

/// <summary>
/// Aggregated performance settings
/// </summary>
public class PerformanceSettings
{
    public CacheSettings Cache { get; set; } = new();
    public RateLimitSettings RateLimit { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public RequestLimitSettings RequestLimits { get; set; } = new();
}
