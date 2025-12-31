using System.Diagnostics;
using EmbeddronicsBackend.Services.Monitoring;
using Serilog;

namespace EmbeddronicsBackend.Middleware;

/// <summary>
/// Middleware for tracking HTTP request performance metrics
/// </summary>
public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPerformanceMonitorService _performanceMonitor;

    public RequestMetricsMiddleware(RequestDelegate next, IPerformanceMonitorService performanceMonitor)
    {
        _next = next;
        _performanceMonitor = performanceMonitor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        // Increment total request counter
        _performanceMonitor.IncrementCounter("http_requests_total");
        _performanceMonitor.IncrementCounter("http_requests_last_minute");
        _performanceMonitor.IncrementCounter($"http_requests_{method.ToLower()}");

        var hasError = false;

        try
        {
            await _next(context);
        }
        catch
        {
            hasError = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record response metrics
            var statusCode = context.Response.StatusCode;
            var statusCategory = statusCode / 100;

            _performanceMonitor.IncrementCounter($"http_responses_{statusCategory}xx");
            
            if (statusCode >= 400)
            {
                hasError = true;
                _performanceMonitor.IncrementCounter("http_errors_total");
            }

            // Record timing metric
            _performanceMonitor.RecordMetric("http_request_duration_ms", durationMs, new Dictionary<string, object>
            {
                { "path", NormalizePath(path) },
                { "method", method },
                { "status_code", statusCode }
            });

            // Log request completion
            var correlationId = context.GetCorrelationId();
            var requestId = context.GetRequestId();

            using (Serilog.Context.LogContext.PushProperty("ResponseTimeMs", durationMs))
            using (Serilog.Context.LogContext.PushProperty("StatusCode", statusCode))
            {
                if (hasError || statusCode >= 400)
                {
                    Log.Warning(
                        "HTTP {Method} {Path} responded {StatusCode} in {Duration:F2}ms [CorrelationId: {CorrelationId}]",
                        method, path, statusCode, durationMs, correlationId);
                }
                else if (durationMs > 1000)
                {
                    Log.Warning(
                        "Slow request: HTTP {Method} {Path} responded {StatusCode} in {Duration:F2}ms [CorrelationId: {CorrelationId}]",
                        method, path, statusCode, durationMs, correlationId);
                }
                else
                {
                    Log.Information(
                        "HTTP {Method} {Path} responded {StatusCode} in {Duration:F2}ms",
                        method, path, statusCode, durationMs);
                }
            }
        }
    }

    /// <summary>
    /// Normalize paths to prevent high cardinality in metrics (remove IDs)
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Replace numeric IDs with placeholder
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            if (int.TryParse(segments[i], out _) || Guid.TryParse(segments[i], out _))
            {
                segments[i] = "{id}";
            }
        }
        return string.Join("/", segments);
    }
}

/// <summary>
/// Extension method to add request metrics middleware to the pipeline
/// </summary>
public static class RequestMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestMetricsMiddleware>();
    }
}
