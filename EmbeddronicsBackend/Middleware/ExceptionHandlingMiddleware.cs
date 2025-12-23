using System.Net;
using System.Text.Json;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Models.Exceptions;
using Serilog;
using Serilog.Events;

namespace EmbeddronicsBackend.Middleware
{
    /// <summary>
    /// Middleware for handling exceptions globally and returning standardized error responses
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestId = context.TraceIdentifier;
            var method = context.Request.Method;
            var path = context.Request.Path;
            var user = context.User?.Identity?.Name ?? "anonymous";

            Log.Information("Processing request {RequestId}: {Method} {Path} by user {User}", 
                requestId, method, path, user);

            try
            {
                await _next(context);
                
                Log.Information("Request {RequestId} completed successfully with status {StatusCode}", 
                    requestId, context.Response.StatusCode);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Request {RequestId} failed with exception. Method: {Method}, Path: {Path}, User: {User}", 
                    requestId, method, path, user);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var traceId = context.TraceIdentifier;
            var response = CreateErrorResponse(exception, traceId);
            
            context.Response.StatusCode = response.StatusCode;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var jsonResponse = JsonSerializer.Serialize(response, jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }

        private ApiResponse<object> CreateErrorResponse(Exception exception, string traceId)
        {
            var response = exception switch
            {
                ValidationException validationEx => ApiResponse<object>.ValidationErrorResponse(validationEx.Errors),
                
                NotFoundException notFoundEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Message = notFoundEx.Message,
                    Errors = new List<string> { notFoundEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                UnauthorizedOperationException unauthorizedEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Message = unauthorizedEx.Message,
                    Errors = new List<string> { unauthorizedEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                UnauthorizedAccessException systemUnauthorizedEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Message = "Unauthorized access",
                    Errors = new List<string> { systemUnauthorizedEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                BusinessRuleException businessEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = businessEx.Message,
                    Errors = new List<string> { businessEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                ArgumentNullException argNullEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Required parameter is missing",
                    Errors = new List<string> { argNullEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                ArgumentException argEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Invalid argument provided",
                    Errors = new List<string> { argEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                InvalidOperationException invalidOpEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Invalid operation",
                    Errors = new List<string> { invalidOpEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                TimeoutException timeoutEx => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.RequestTimeout,
                    Message = "Request timeout",
                    Errors = new List<string> { timeoutEx.Message },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                },
                
                _ => new ApiResponse<object>
                {
                    Success = false,
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Message = "An internal server error occurred",
                    Errors = new List<string> { "An unexpected error occurred. Please try again later." },
                    TraceId = traceId,
                    Timestamp = DateTime.UtcNow
                }
            };

            // Log different exception types with appropriate log levels
            LogException(exception, response.StatusCode);

            return response;
        }

        private void LogException(Exception exception, int statusCode)
        {
            var logLevel = statusCode switch
            {
                >= 500 => LogEventLevel.Error,
                >= 400 => LogEventLevel.Warning,
                _ => LogEventLevel.Information
            };

            Log.Write(logLevel, exception, 
                "Exception handled by middleware. Status Code: {StatusCode}, Exception Type: {ExceptionType}", 
                statusCode, exception.GetType().Name);
        }
    }

    /// <summary>
    /// Extension method to register the exception handling middleware
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}