using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Models.Exceptions;
using EmbeddronicsBackend.Attributes;
using Serilog;

namespace EmbeddronicsBackend.Controllers
{
    /// <summary>
    /// Test controller to demonstrate exception handling and authorization
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : BaseApiController
    {
        [HttpGet("success")]
        public ActionResult<ApiResponse<string>> TestSuccess()
        {
            Log.Information("Test success endpoint called");
            return Success("Test successful!", "Success response test");
        }

        [HttpGet("validation-error")]
        public ActionResult<ApiResponse<string>> TestValidationError()
        {
            Log.Information("Test validation error endpoint called");
            throw new ValidationException("testField", "This is a test validation error");
        }

        [HttpGet("not-found")]
        public ActionResult<ApiResponse<string>> TestNotFound()
        {
            Log.Information("Test not found endpoint called");
            throw new NotFoundException("TestResource", 123);
        }

        [HttpGet("business-rule")]
        public ActionResult<ApiResponse<string>> TestBusinessRule()
        {
            Log.Information("Test business rule endpoint called");
            throw new BusinessRuleException("TEST_RULE", "This business rule was violated for testing");
        }

        [HttpGet("unauthorized")]
        public ActionResult<ApiResponse<string>> TestUnauthorized()
        {
            Log.Information("Test unauthorized endpoint called");
            throw new UnauthorizedOperationException("Test unauthorized access");
        }

        [HttpGet("server-error")]
        public ActionResult<ApiResponse<string>> TestServerError()
        {
            Log.Information("Test server error endpoint called");
            throw new InvalidOperationException("This is a test server error");
        }

        [HttpGet("admin-only")]
        [AdminOnly]
        public ActionResult<ApiResponse<string>> TestAdminOnly()
        {
            Log.Information("Admin-only endpoint accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            return Success("Admin access granted!", "Admin-only endpoint test");
        }

        [HttpGet("authenticated-user")]
        [AuthenticatedUser]
        public ActionResult<ApiResponse<string>> TestAuthenticatedUser()
        {
            Log.Information("Authenticated user endpoint accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            return Success("Authenticated access granted!", "Authenticated user endpoint test");
        }

        [HttpGet("client-only")]
        [ClientOnly]
        public ActionResult<ApiResponse<string>> TestClientOnly()
        {
            Log.Information("Client-only endpoint accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            return Success("Client access granted!", "Client-only endpoint test");
        }
    }
}