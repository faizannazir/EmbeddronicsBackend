using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly IDataService<Service> _serviceService;

        public ServicesController(IDataService<Service> serviceService)
        {
            _serviceService = serviceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            Serilog.Log.Information("Services list accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            var services = await _serviceService.GetAllAsync();
            return Ok(services);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            Serilog.Log.Information("Service {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var service = await _serviceService.GetByIdAsync(id);
            if (service == null) return NotFound();
            return Ok(service);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Service service)
        {
            Serilog.Log.Information("Creating new service by user: {User}", User?.Identity?.Name ?? "anonymous");
            var created = await _serviceService.CreateAsync(service);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Service service)
        {
            Serilog.Log.Information("Updating service {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var updated = await _serviceService.UpdateAsync(id, service);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            Serilog.Log.Information("Deleting service {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var result = await _serviceService.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
