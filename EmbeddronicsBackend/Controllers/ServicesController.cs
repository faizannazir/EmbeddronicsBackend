using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Authorization.Attributes;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : BaseApiController
    {
        private readonly IDataService<Service> _serviceService;

        public ServicesController(IDataService<Service> serviceService)
        {
            _serviceService = serviceService;
        }

        [HttpGet]
        [AllowAnonymous] // Public access for services catalog
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)] // Cache for 5 minutes
        public async Task<ActionResult<ApiResponse<IEnumerable<Service>>>> GetAll()
        {
            Serilog.Log.Information("Services list accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            var services = await _serviceService.GetAllAsync();
            
            // Add cache headers for public content
            Response.Headers["Cache-Control"] = "public, max-age=300";
            Response.Headers["ETag"] = $"\"{services.GetHashCode()}\"";
            
            return Success(services, "Services retrieved successfully");
        }

        [HttpGet("{id}")]
        [AllowAnonymous] // Public access for individual services
        [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)] // Cache for 10 minutes
        public async Task<ActionResult<ApiResponse<Service>>> GetById(int id)
        {
            Serilog.Log.Information("Service {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var service = await _serviceService.GetByIdAsync(id);
            if (service == null) 
            {
                return NotFound<Service>("Service not found");
            }
            
            // Add cache headers for public content
            Response.Headers["Cache-Control"] = "public, max-age=600";
            Response.Headers["ETag"] = $"\"{service.GetHashCode()}\"";
            
            return Success(service, "Service retrieved successfully");
        }

        [HttpPost]
        [AdminCRM] // Admin-only access for creating services
        public async Task<ActionResult<ApiResponse<Service>>> Create([FromBody] Service service)
        {
            Serilog.Log.Information("Creating new service by user: {User}", User?.Identity?.Name ?? "anonymous");
            var created = await _serviceService.CreateAsync(service);
            return Success(created, "Service created successfully");
        }

        [HttpPut("{id}")]
        [AdminCRM] // Admin-only access for updating services
        public async Task<ActionResult<ApiResponse<Service>>> Update(int id, [FromBody] Service service)
        {
            Serilog.Log.Information("Updating service {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var updated = await _serviceService.UpdateAsync(id, service);
            if (updated == null) 
            {
                return NotFound<Service>("Service not found");
            }
            return Success(updated, "Service updated successfully");
        }

        [HttpDelete("{id}")]
        [AdminCRM] // Admin-only access for deleting services
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            Serilog.Log.Information("Deleting service {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var result = await _serviceService.DeleteAsync(id);
            if (!result) 
            {
                return NotFound<bool>("Service not found");
            }
            return Success(true, "Service deleted successfully");
        }
    }
}
