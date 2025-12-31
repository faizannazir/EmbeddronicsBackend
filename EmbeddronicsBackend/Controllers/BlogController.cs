using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Authorization.Attributes;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BlogController : BaseApiController
    {
        private readonly IDataService<BlogPost> _blogService;

        public BlogController(IDataService<BlogPost> blogService)
        {
            _blogService = blogService;
        }

        [HttpGet]
        [AllowAnonymous] // Public access for blog posts
        [ResponseCache(Duration = 180, Location = ResponseCacheLocation.Any)] // Cache for 3 minutes
        public async Task<ActionResult<ApiResponse<IEnumerable<BlogPost>>>> GetAll()
        {
            Serilog.Log.Information("Blog posts accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            var posts = await _blogService.GetAllAsync();
            
            // Add cache headers for public content
            Response.Headers["Cache-Control"] = "public, max-age=180";
            Response.Headers["ETag"] = $"\"{posts.GetHashCode()}\"";
            
            return Success(posts, "Blog posts retrieved successfully");
        }

        [HttpGet("{id}")]
        [AllowAnonymous] // Public access for individual blog posts
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)] // Cache for 5 minutes
        public async Task<ActionResult<ApiResponse<BlogPost>>> GetById(int id)
        {
            Serilog.Log.Information("Blog post {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var post = await _blogService.GetByIdAsync(id);
            if (post == null) 
            {
                return NotFound<BlogPost>("Blog post not found");
            }
            
            // Increment views
            post.Views++;
            await _blogService.UpdateAsync(id, post);
            
            // Add cache headers for public content
            Response.Headers["Cache-Control"] = "public, max-age=300";
            Response.Headers["ETag"] = $"\"{post.GetHashCode()}\"";
            
            return Success(post, "Blog post retrieved successfully");
        }

        [HttpPost]
        [AdminCRM] // Admin-only access for creating blog posts
        public async Task<ActionResult<ApiResponse<BlogPost>>> Create([FromBody] BlogPost post)
        {
            Serilog.Log.Information("Creating new blog post by user: {User}", User?.Identity?.Name ?? "anonymous");
            var created = await _blogService.CreateAsync(post);
            return Success(created, "Blog post created successfully");
        }

        [HttpPut("{id}")]
        [AdminCRM] // Admin-only access for updating blog posts
        public async Task<ActionResult<ApiResponse<BlogPost>>> Update(int id, [FromBody] BlogPost post)
        {
            Serilog.Log.Information("Updating blog post {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var updated = await _blogService.UpdateAsync(id, post);
            if (updated == null) 
            {
                return NotFound<BlogPost>("Blog post not found");
            }
            return Success(updated, "Blog post updated successfully");
        }

        [HttpDelete("{id}")]
        [AdminCRM] // Admin-only access for deleting blog posts
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            Serilog.Log.Information("Deleting blog post {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var result = await _blogService.DeleteAsync(id);
            if (!result) 
            {
                return NotFound<bool>("Blog post not found");
            }
            return Success(true, "Blog post deleted successfully");
        }
    }
}
