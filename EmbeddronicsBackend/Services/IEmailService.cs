using EmbeddronicsBackend.Controllers;

namespace EmbeddronicsBackend.Services
{
    public interface IEmailService
    {
        Task<bool> SendContactFormEmailAsync(ContactRequest request);
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false);
        Task<bool> SendWelcomeEmailAsync(string email, string name);
        Task<bool> SendOrderStatusUpdateEmailAsync(string email, string orderTitle, string status);
        Task<bool> SendQuoteNotificationEmailAsync(string email, string orderTitle, decimal amount);
    }
}