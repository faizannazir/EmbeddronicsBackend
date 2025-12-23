using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Controllers
{
    /// <summary>
    /// Base controller providing common functionality for API controllers
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Returns a successful response with data
        /// </summary>
        protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "Success")
        {
            var response = ApiResponse<T>.SuccessResponse(data, message);
            return Ok(response);
        }

        /// <summary>
        /// Returns a successful response without data
        /// </summary>
        protected ActionResult<ApiResponse> Success(string message = "Success")
        {
            var response = ApiResponse.SuccessResponse(message);
            return Ok(response);
        }

        /// <summary>
        /// Returns a bad request response
        /// </summary>
        protected ActionResult<ApiResponse<T>> BadRequest<T>(string message)
        {
            var response = ApiResponse<T>.ErrorResponse(message, 400);
            return BadRequest(response);
        }

        /// <summary>
        /// Returns a not found response
        /// </summary>
        protected ActionResult<ApiResponse<T>> NotFound<T>(string message = "Resource not found")
        {
            var response = ApiResponse<T>.NotFoundResponse(message);
            return NotFound(response);
        }

        /// <summary>
        /// Returns an unauthorized response
        /// </summary>
        protected ActionResult<ApiResponse<T>> Unauthorized<T>(string message = "Unauthorized access")
        {
            var response = ApiResponse<T>.UnauthorizedResponse(message);
            return Unauthorized(response);
        }

        /// <summary>
        /// Returns a forbidden response
        /// </summary>
        protected ActionResult<ApiResponse<T>> Forbidden<T>(string message = "Access forbidden")
        {
            var response = ApiResponse<T>.ForbiddenResponse(message);
            return StatusCode(403, response);
        }

        /// <summary>
        /// Returns an internal server error response
        /// </summary>
        protected ActionResult<ApiResponse<T>> InternalServerError<T>(string message = "An internal server error occurred")
        {
            var response = ApiResponse<T>.InternalServerErrorResponse(message);
            return StatusCode(500, response);
        }
    }
}