using Microsoft.AspNetCore.Mvc;
using EmbeddronicsBackend.Models;
using EmbeddronicsBackend.Services;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BlogController : ControllerBase
    {
        private readonly IDataService<BlogPost> _blogService;

        public BlogController(IDataService<BlogPost> blogService)
        {
            _blogService = blogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            Serilog.Log.Information("Blog posts accessed by user: {User}", User?.Identity?.Name ?? "anonymous");
            var posts = await _blogService.GetAllAsync();
            return Ok(posts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            Serilog.Log.Information("Blog post {Id} accessed by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var post = await _blogService.GetByIdAsync(id);
            if (post == null) return NotFound();
            
            // Increment views
            post.Views++;
            await _blogService.UpdateAsync(id, post);
            
            return Ok(post);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BlogPost post)
        {
            Serilog.Log.Information("Creating new blog post by user: {User}", User?.Identity?.Name ?? "anonymous");
            var created = await _blogService.CreateAsync(post);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BlogPost post)
        {
            Serilog.Log.Information("Updating blog post {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var updated = await _blogService.UpdateAsync(id, post);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            Serilog.Log.Information("Deleting blog post {Id} by user: {User}", id, User?.Identity?.Name ?? "anonymous");
            var result = await _blogService.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
