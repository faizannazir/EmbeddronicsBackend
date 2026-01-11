using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace EmbeddronicsBackend.Middleware
{
    public class ResponseStartLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ResponseStartLoggingMiddleware> _logger;

        public ResponseStartLoggingMiddleware(RequestDelegate next, ILogger<ResponseStartLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Register a callback that will run right before headers are sent
            context.Response.OnStarting(state =>
            {
                var httpContext = (HttpContext)state!;
                try
                {
                    _logger.LogWarning("Response.OnStarting fired for {Method} {Path}. Response.HasStarted={HasStarted}, StatusCode={StatusCode}",
                        httpContext.Request.Method,
                        httpContext.Request.Path,
                        httpContext.Response.HasStarted,
                        httpContext.Response.StatusCode);

                    // Log a short stack trace to help identify which code path triggered the start
                    _logger.LogWarning("Response start stack trace:\n{Stack}", Environment.StackTrace);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while logging Response.OnStarting");
                }
                return Task.CompletedTask;
            }, context);

            await _next(context);
        }
    }

    public static class ResponseStartLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseResponseStartLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ResponseStartLoggingMiddleware>();
        }
    }
}
