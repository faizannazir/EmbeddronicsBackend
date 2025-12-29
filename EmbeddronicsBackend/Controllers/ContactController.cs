using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        [HttpPost]
        [AllowAnonymous] // Allow anonymous access for contact form
        public IActionResult SendMessage([FromBody] ContactRequest request)
        {
            Serilog.Log.Information("Contact form submitted by {Name} ({Email})", request.Name, request.Email);
            // In production, send email or save to database
            return Ok(new { message = "Your message has been sent successfully!" });
        }
    }

    public class ContactRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        [Required]
        public string Message { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
    }
}
