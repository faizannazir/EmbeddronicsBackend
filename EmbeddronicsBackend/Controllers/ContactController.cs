using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using EmbeddronicsBackend.Services;
using EmbeddronicsBackend.Models;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : BaseApiController
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<ContactController> _logger;

        public ContactController(IEmailService emailService, ILogger<ContactController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous] // Allow anonymous access for contact form
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] // No caching for form submissions
        public async Task<ActionResult<ApiResponse<object>>> SendMessage([FromBody] ContactRequest request)
        {
            try
            {
                _logger.LogInformation("Contact form submitted by {Name} ({Email})", request.Name, request.Email);
                
                // Send email notification to admin
                var emailSent = await _emailService.SendContactFormEmailAsync(request);
                
                if (emailSent)
                {
                    return Success(
                        (object)new { message = "Your message has been sent successfully!" }, 
                        "Contact form submitted successfully"
                    );
                }
                else
                {
                    _logger.LogWarning("Failed to send contact form email for {Name} ({Email})", request.Name, request.Email);
                    return Success(
                        (object)new { message = "Your message has been received and will be processed shortly." }, 
                        "Contact form submitted successfully"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing contact form submission from {Name} ({Email})", request.Name, request.Email);
                return InternalServerError<object>("An error occurred while processing your message. Please try again later.");
            }
        }
    }

    public class ContactRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please provide a valid email address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string Email { get; set; } = string.Empty;
        
        [Phone(ErrorMessage = "Please provide a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string Phone { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Message is required")]
        [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters")]
        [MinLength(10, ErrorMessage = "Message must be at least 10 characters long")]
        public string Message { get; set; } = string.Empty;
        
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
        public string Subject { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters")]
        public string Company { get; set; } = string.Empty;
    }
}
