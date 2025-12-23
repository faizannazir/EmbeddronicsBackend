using EmbeddronicsBackend.Models.Exceptions;

namespace EmbeddronicsBackend.Models
{
    /// <summary>
    /// Standardized API response wrapper for consistent response format
    /// </summary>
    /// <typeparam name="T">The type of data being returned</typeparam>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }
        public int StatusCode { get; set; }
        public string TraceId { get; set; }
        public DateTime Timestamp { get; set; }

        public ApiResponse()
        {
            Message = string.Empty;
            Errors = new List<string>();
            TraceId = string.Empty;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a successful response with data
        /// </summary>
        public static ApiResponse<T> SuccessResponse(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message,
                StatusCode = 200,
                Errors = new List<string>()
            };
        }

        /// <summary>
        /// Creates a successful response without data
        /// </summary>
        public static ApiResponse<T> SuccessResponse(string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = default(T),
                Message = message,
                StatusCode = 200,
                Errors = new List<string>()
            };
        }

        /// <summary>
        /// Creates an error response with a single error message
        /// </summary>
        public static ApiResponse<T> ErrorResponse(string message, int statusCode = 400)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = message,
                StatusCode = statusCode,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates an error response with multiple error messages
        /// </summary>
        public static ApiResponse<T> ErrorResponse(List<string> errors, string message = "An error occurred", int statusCode = 400)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = message,
                StatusCode = statusCode,
                Errors = errors ?? new List<string>()
            };
        }

        /// <summary>
        /// Creates an error response from validation errors
        /// </summary>
        public static ApiResponse<T> ValidationErrorResponse(List<ValidationError> validationErrors)
        {
            var errors = validationErrors?.Select(e => $"{e.Field}: {e.Message}").ToList() ?? new List<string>();
            
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = "Validation failed",
                StatusCode = 400,
                Errors = errors
            };
        }

        /// <summary>
        /// Creates a not found error response
        /// </summary>
        public static ApiResponse<T> NotFoundResponse(string message = "Resource not found")
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = message,
                StatusCode = 404,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates an unauthorized error response
        /// </summary>
        public static ApiResponse<T> UnauthorizedResponse(string message = "Unauthorized access")
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = message,
                StatusCode = 401,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a forbidden error response
        /// </summary>
        public static ApiResponse<T> ForbiddenResponse(string message = "Access forbidden")
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = message,
                StatusCode = 403,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates an internal server error response
        /// </summary>
        public static ApiResponse<T> InternalServerErrorResponse(string message = "An internal server error occurred")
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default(T),
                Message = message,
                StatusCode = 500,
                Errors = new List<string> { message }
            };
        }
    }

    /// <summary>
    /// Non-generic version of ApiResponse for responses without data
    /// </summary>
    public class ApiResponse : ApiResponse<object>
    {
        /// <summary>
        /// Creates a successful response without data
        /// </summary>
        public static new ApiResponse SuccessResponse(string message = "Success")
        {
            return new ApiResponse
            {
                Success = true,
                Data = null,
                Message = message,
                StatusCode = 200,
                Errors = new List<string>()
            };
        }

        /// <summary>
        /// Creates an error response with a single error message
        /// </summary>
        public static new ApiResponse ErrorResponse(string message, int statusCode = 400)
        {
            return new ApiResponse
            {
                Success = false,
                Data = null,
                Message = message,
                StatusCode = statusCode,
                Errors = new List<string> { message }
            };
        }
    }
}