namespace EmbeddronicsBackend.Middleware;

/// <summary>
/// Middleware for HTTP response caching and ETags support
/// </summary>
public class ResponseCachingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseCachingMiddleware> _logger;

    public ResponseCachingMiddleware(RequestDelegate next, ILogger<ResponseCachingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip caching for non-GET requests
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip caching for authenticated requests (private data)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.Headers["Cache-Control"] = "private, no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            await _next(context);
            return;
        }

        // For public endpoints, add caching headers if not already set
        await _next(context);

        // Only add cache headers to successful responses that don't already have them
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 &&
            !context.Response.Headers.ContainsKey("Cache-Control"))
        {
            // Default public cache for anonymous GET requests
            context.Response.Headers["Cache-Control"] = "public, max-age=300";
        }
    }
}

/// <summary>
/// Extension methods for response caching middleware
/// </summary>
public static class ResponseCachingMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomResponseCaching(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ResponseCachingMiddleware>();
    }
}
