using Microsoft.AspNetCore.Mvc;

namespace EmbeddronicsBackend.Attributes;

/// <summary>
/// Attribute for HTTP response caching on public endpoints
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class CacheResponseAttribute : ResponseCacheAttribute
{
    public CacheResponseAttribute(int durationSeconds = 300)
    {
        Duration = durationSeconds;
        Location = ResponseCacheLocation.Any;
        VaryByHeader = "Accept-Encoding";
    }
}

/// <summary>
/// Attribute to cache response for public endpoints only
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class PublicCacheAttribute : ResponseCacheAttribute
{
    public PublicCacheAttribute(int durationSeconds = 600)
    {
        Duration = durationSeconds;
        Location = ResponseCacheLocation.Any;
        VaryByHeader = "Accept-Encoding";
        VaryByQueryKeys = new[] { "*" }; // Vary by all query parameters
    }
}

/// <summary>
/// Attribute to prevent caching on sensitive endpoints
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class NoCacheAttribute : ResponseCacheAttribute
{
    public NoCacheAttribute()
    {
        NoStore = true;
        Duration = 0;
        Location = ResponseCacheLocation.None;
    }
}

/// <summary>
/// Cache profile names for consistent caching configuration
/// </summary>
public static class CacheProfiles
{
    public const string Default = "Default";
    public const string Static = "Static";
    public const string Short = "Short";
    public const string NoCache = "NoCache";
    public const string Private = "Private";
}
