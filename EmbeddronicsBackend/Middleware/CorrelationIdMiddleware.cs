using Serilog.Context;

namespace EmbeddronicsBackend.Middleware;

/// <summary>
/// Middleware for correlation ID management.
/// Tracks requests across services and logs for distributed tracing.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string RequestIdHeaderName = "X-Request-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate correlation ID (persists across service calls)
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Generate unique request ID for this specific request
        var requestId = Guid.NewGuid().ToString("N")[..12];

        // Add to response headers for client tracking
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            context.Response.Headers[RequestIdHeaderName] = requestId;
            return Task.CompletedTask;
        });

        // Store in HttpContext for access throughout the request
        context.Items["CorrelationId"] = correlationId;
        context.Items["RequestId"] = requestId;

        // Push to Serilog context for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        using (LogContext.PushProperty("UserAgent", context.Request.Headers.UserAgent.ToString()))
        using (LogContext.PushProperty("ClientIP", GetClientIpAddress(context)))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check for existing correlation ID from upstream service
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) 
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        // Generate new correlation ID
        return Guid.NewGuid().ToString("D");
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers (load balancer/proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Extension method to access correlation ID from HttpContext
/// </summary>
public static class CorrelationIdExtensions
{
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items["CorrelationId"] as string;
    }

    public static string? GetRequestId(this HttpContext context)
    {
        return context.Items["RequestId"] as string;
    }
}

/// <summary>
/// Extension method to add correlation ID middleware to the pipeline
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
