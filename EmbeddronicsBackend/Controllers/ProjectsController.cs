using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Authorization.Attributes;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : BaseApiController
    {
        private readonly IDataService<Project> _projectService;

        public ProjectsController(IDataService<Project> projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        [AllowAnonymous] // Public access for projects showcase
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)] // Cache for 5 minutes
        public async Task<ActionResult<ApiResponse<IEnumerable<Project>>>> GetAll()
        {
            Serilog.Log.Information("Projects list accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            var projects = await _projectService.GetAllAsync();
            
            // Add cache headers for public content
            Response.Headers["Cache-Control"] = "public, max-age=300";
            Response.Headers["ETag"] = $"\"{projects.GetHashCode()}\"";
            
            return Success(projects, "Projects retrieved successfully");
        }

        [HttpGet("{id}")]
        [AllowAnonymous] // Public access for individual projects
        [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)] // Cache for 10 minutes
        public async Task<ActionResult<ApiResponse<Project>>> GetById(int id)
        {
            Serilog.Log.Information("Project {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var project = await _projectService.GetByIdAsync(id);
            if (project == null) 
            {
                return NotFound<Project>("Project not found");
            }
            
            // Add cache headers for public content
            Response.Headers["Cache-Control"] = "public, max-age=600";
            Response.Headers["ETag"] = $"\"{project.GetHashCode()}\"";
            
            return Success(project, "Project retrieved successfully");
        }

        [HttpPost]
        [AdminCRM] // Admin-only access for creating projects
        public async Task<ActionResult<ApiResponse<Project>>> Create([FromBody] Project project)
        {
            Serilog.Log.Information("Creating new project by user: {User}", User?.Identity?.Name ?? "anonymous");
            var created = await _projectService.CreateAsync(project);
            return Success(created, "Project created successfully");
        }

        [HttpPut("{id}")]
        [AdminCRM] // Admin-only access for updating projects
        public async Task<ActionResult<ApiResponse<Project>>> Update(int id, [FromBody] Project project)
        {
            Serilog.Log.Information("Updating project {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var updated = await _projectService.UpdateAsync(id, project);
            if (updated == null) 
            {
                return NotFound<Project>("Project not found");
            }
            return Success(updated, "Project updated successfully");
        }

        [HttpDelete("{id}")]
        [AdminCRM] // Admin-only access for deleting projects
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            Serilog.Log.Information("Deleting project {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var result = await _projectService.DeleteAsync(id);
            if (!result) 
            {
                return NotFound<bool>("Project not found");
            }
            return Success(true, "Project deleted successfully");
        }
    }
}
