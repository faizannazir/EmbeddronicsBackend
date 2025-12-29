using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Authorization.Attributes;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IDataService<Project> _projectService;

        public ProjectsController(IDataService<Project> projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        [AllowAnonymous] // Public access for projects showcase
        public async Task<IActionResult> GetAll()
        {
            Serilog.Log.Information("Projects list accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            var projects = await _projectService.GetAllAsync();
            return Ok(projects);
        }

        [HttpGet("{id}")]
        [AllowAnonymous] // Public access for individual projects
        public async Task<IActionResult> GetById(int id)
        {
            Serilog.Log.Information("Project {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var project = await _projectService.GetByIdAsync(id);
            if (project == null) return NotFound();
            return Ok(project);
        }

        [HttpPost]
        [AdminCRM] // Admin-only access for creating projects
        public async Task<IActionResult> Create([FromBody] Project project)
        {
            Serilog.Log.Information("Creating new project by user: {User}", User?.Identity?.Name ?? "anonymous");
            var created = await _projectService.CreateAsync(project);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        [AdminCRM] // Admin-only access for updating projects
        public async Task<IActionResult> Update(int id, [FromBody] Project project)
        {
            Serilog.Log.Information("Updating project {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var updated = await _projectService.UpdateAsync(id, project);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        [AdminCRM] // Admin-only access for deleting projects
        public async Task<IActionResult> Delete(int id)
        {
            Serilog.Log.Information("Deleting project {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var result = await _projectService.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
